# Tasks: Family Siblings

**Input**: Design documents from `specs/030-family-siblings/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required by constitution Principle V (real PostgreSQL via TestContainers for backend
integration tests; component tests for web/parent-mobile), same standard every prior feature has
followed.

**Organization**: Tasks are grouped by user story to enable independent implementation and
testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Contracts DTOs and i18n scaffolding shared across stories.

- [X] T001 [P] Add `BulkDayReservationRequest` (`{ childIds, type, requestedDate,
  exchangeForDate, reason }`) and `BulkDayReservationResponse` (`{ results: [{ childId,
  childName, succeeded, reservation?, errorKey? }] }`) per contracts/family-siblings-api.md in
  `backend/ChildCare.Contracts/Requests/DayReservationRequests.cs` and
  `backend/ChildCare.Contracts/Responses/DayReservationResponses.cs`
- [X] T002 [P] Add `UpdateLocationSiblingBillingSettingsRequest` (`{ siblingDiscountPct,
  familyInvoiceBundlingEnabled }`) and extend `LocationResponse` with the same two fields in
  `backend/ChildCare.Contracts/Requests/LocationRequests.cs` and
  `backend/ChildCare.Contracts/Responses/LocationResponse.cs`
- [X] T003 [P] Extend `InvoiceResponse` with nullable `FamilyGroupId`; add
  `FamilyInvoiceResponse` (`{ familyGroupId, children: [{ childId, childName, subtotalCents }],
  totalCents, status, dueDate }`) per contracts/family-siblings-api.md in
  `backend/ChildCare.Contracts/Responses/InvoiceResponse.cs`
- [X] T004 [P] Add `ParentPreviousChildResponse` (`ParentChildResponse` shape +
  `enrollmentStart`/`enrollmentEnd`) in `backend/ChildCare.Contracts/Responses/ParentResponses.cs`
- [X] T005 [P] Add parent-mobile `dayReservations.applyToAllChildren`,
  `dayReservations.bulkPartialResult` i18n keys to `parent-mobile/i18n/locales/en.json`,
  `parent-mobile/i18n/locales/fr.json`, `parent-mobile/i18n/locales/nl.json`
- [X] T006 [P] Add parent-mobile `previousChildren.*` (title, empty enrollment period label,
  read-only banner) and `invoices.familyGroup.*` (combined total label, per-child line label) i18n
  keys to the same three parent-mobile locale files
- [X] T007 [P] Add web `locations.siblingBilling.*` (discount percent field, bundling toggle,
  save) i18n keys to `web/i18n/locales/en.json`, `web/i18n/locales/fr.json`,
  `web/i18n/locales/nl.json`
- [X] T008 [P] Add web `children.contacts.*` (tab label, relationship options incl. foster
  parent/other, primary badge, add/remove/set-primary actions, duplicate-match suggestion
  copy, empty state) i18n keys to the same three web locale files

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Entity/enum extensions every user story depends on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T009 [P] Add `SiblingDiscountPct` (`decimal(5,2)`, default 0) and
  `FamilyInvoiceBundlingEnabled` (`bool`, default false) to
  `backend/ChildCare.Domain/Entities/Location.cs` per data-model.md
- [X] T010 [P] Add nullable `FamilyGroupId` (`Guid?`) to
  `backend/ChildCare.Domain/Entities/Invoice.cs` per data-model.md
- [X] T011 [P] Append `FosterParent`, `Other` to
  `backend/ChildCare.Domain/Enums/ContactRelationship.cs` per research.md R6
- [X] T012 Configure the new `Location`/`Invoice` columns in
  `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs` (depends on T009, T010)
- [X] T013 Add tenant migration `AddSiblingBillingSettingsAndFamilyGroupId` in
  `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` (depends on T012)
- [X] T014 Extend `TenantMigrationRolloutTests`' schema-revert helper for the new `Location`/
  `Invoice` columns (the recurring pattern every migration-adding feature has needed) in
  `backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs` (depends on T013)

**Checkpoint**: Schema ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Report all children absent in one action (Priority: P1) 🎯 MVP

**Goal**: A parent with 2+ active linked children can submit one absence/extra/exchange request
that fans out to independent per-child reservations.

**Independent Test**: Submit a bulk absence request for 2 linked children at the same location;
confirm two independent `DayReservation` rows exist, each subject to its own location's 013f
settings.

### Tests for User Story 1

- [X] T015 [P] [US1] Test: bulk submission with 2 active children creates one reservation per
  child, both `Approved`/`Pending` per each location's own policy, in
  `backend/ChildCare.Api.Tests/DayReservations/BulkDayReservationTests.cs`
- [X] T016 [P] [US1] Test: bulk submission where one child's location has the request type
  disabled (013f) creates a reservation for the allowed child and reports the other as skipped
  with `errors.day_reservations.request_type_disabled`, in the same test file
- [X] T017 [P] [US1] Test: bulk submission including a `childId` the caller isn't linked to
  returns that entry as failed (`errors.day_reservations.child_not_linked`) without blocking the
  other children, in the same test file
- [X] T018 [P] [US1] Test: siblings at two different locations each evaluate their own location's
  notice-hours/closure-day rules independently, in the same test file

### Implementation for User Story 1

- [X] T019 [US1] Implement `SubmitBulkDayReservationCommand` (dispatches the existing
  `SubmitDayReservationCommand` per child via `IMediator.Send`, aggregates per-child
  success/failure) per research.md R1 in
  `backend/ChildCare.Application/DayReservations/SubmitBulkDayReservationCommand.cs` (depends on
  T001)
- [X] T020 [US1] Add `FluentValidation` validator (non-empty `ChildIds`, same field rules
  `SubmitDayReservationCommandValidator` already applies) in the same file
- [X] T021 [US1] Add `POST /api/parent/day-reservations/bulk` route per
  contracts/family-siblings-api.md in
  `backend/ChildCare.Api/Endpoints/DayReservationEndpoints.cs` (depends on T019)
- [X] T022 [P] [US1] Add "apply to all my children" toggle to
  `parent-mobile/components/DayReservationForm.tsx` — shown only when 2+ active children are
  linked, switches submission to the new bulk endpoint, and renders per-child partial-failure
  results (T005/T006 i18n keys)
- [X] T023 [P] [US1] Add `submitBulkDayReservation` to `parent-mobile/services/dayReservations.ts`
- [X] T024 [P] [US1] Component test: form shows the bulk toggle only for 2+ children, and renders
  a partial-failure result correctly, in `parent-mobile/__tests__/dayReservations.test.tsx`

**Checkpoint**: Bulk day-reservation submission works end to end — User Story 1 independently
testable/demoable.

---

## Phase 4: User Story 2 - Sibling discount applied automatically on invoices (Priority: P2)

**Goal**: A location-configured sibling discount automatically applies to every child but the
earliest-enrolled one, among children sharing the same primary contact at that location.

**Independent Test**: Configure a 10% discount on a location, generate invoices for a 2-child
family, confirm the later-enrolled child's invoice carries a labeled discount line and the
other doesn't.

### Tests for User Story 2

- [X] T025 [P] [US2] Test: 2 siblings, same primary contact, same location, 10% discount
  configured → later-contract child's invoice has a `-10%` discount line item, earlier-contract
  child's invoice has none, in
  `backend/ChildCare.Api.Tests/Invoices/SiblingDiscountAndBundlingTests.cs`
- [X] T026 [P] [US2] Test: discount = 0 (default) → no discount line on any invoice, in the same
  test file
- [X] T027 [P] [US2] Test: 2 children of the same parent at two different locations → no discount
  at either (no shared-location sibling group), in the same test file
- [X] T028 [P] [US2] Test: 2 children with no shared primary contact (unrelated families,
  same location) → no discount, in the same test file
- [X] T029 [P] [US2] Test: 3 siblings same primary contact/location → discount applies to all but
  the earliest-contract child, in the same test file

### Implementation for User Story 2

- [X] T030 [US2] Extend `GenerateInvoicesCommand` to group same-period contracts-being-invoiced
  by child's primary `ChildContact.ContactId`, and for any group of 2+ at a location with
  `SiblingDiscountPct > 0`, append a discount `ExtraCharge` (negative `AmountCents`, i18n key
  `invoices.lineItems.siblingDiscount`) to every child but the earliest `Contract.StartDate`, per
  research.md R2/R3 in `backend/ChildCare.Application/Invoices/GenerateInvoicesCommand.cs`
  (depends on T009)
- [X] T031 [US2] Implement `UpdateLocationSiblingBillingSettingsCommand` (mirrors
  `UpdateLocationInvoiceSettingsCommand`) in
  `backend/ChildCare.Application/Locations/UpdateLocationSiblingBillingSettingsCommand.cs`
  (depends on T002, T009)
- [X] T032 [US2] Add `PUT /api/locations/{locationId}/sibling-billing-settings` route in
  `backend/ChildCare.Api/Endpoints/LocationEndpoints.cs` (depends on T031)
- [ ] T033 [P] [US2] Add sibling-discount percent field to
  `web/components/InvoiceSettingsForm.tsx` (extends the existing Invoicing tab per plan.md)
- [ ] T034 [P] [US2] Component test: saving the discount field calls the new settings endpoint,
  in `web/__tests__/InvoiceSettingsForm.test.tsx` (or existing equivalent test file for that
  component)

**Checkpoint**: Sibling discount applies automatically and is independently demoable/testable.

---

## Phase 5: User Story 4 - Director sees and manages a child's linked family contacts (Priority: P2)

**Goal**: A first web UI for the already-existing (006/013) contact-linking endpoints, with
duplicate-contact detection.

**Independent Test**: Open a child's profile, see linked contacts with relationship/primary flag;
add a contact whose email matches an existing one, confirm the UI offers to link instead of
duplicating.

### Tests for User Story 4

- [ ] T035 [P] [US4] Component test: Contacts tab renders linked contacts with relationship and
  primary badge, and an empty state with zero contacts, in
  `web/__tests__/children/ChildContactsTab.test.tsx`
- [ ] T036 [P] [US4] Component test: adding a contact whose email matches an existing tenant
  contact surfaces a "link existing contact instead" suggestion; proceeding with a non-matching
  entry creates + links a new contact, in
  `web/__tests__/children/LinkContactDialog.test.tsx`
- [ ] T037 [P] [US4] Component test: changing which contact is primary calls the existing
  update-link endpoint and reflects the new primary in the tab, in the same test file as T035

### Implementation for User Story 4

- [ ] T038 [US4] Add a "Contacts" tab to
  `web/app/(app)/children/[id]/page.tsx` alongside the existing Profile/Health/Milestones tabs
  (plan.md)
- [ ] T039 [US4] Implement `web/components/children/ChildContactsTab.tsx` — lists
  `GET /api/children/{childId}/contacts`, relationship + primary badge, remove action
  (`DELETE .../contacts/{contactId}`), set-primary action (`PUT .../contacts/{contactId}` with
  `isPrimary: true`), including the new `fosterParent`/`other` relationship options (depends on
  T008, T011)
- [ ] T040 [US4] Implement `web/components/children/LinkContactDialog.tsx` — fetches
  `GET /api/contacts` once, filters client-side for an email/phone match as the director types
  (research.md R7), offers "link existing" (`POST .../contacts` with the matched `contactId`) vs.
  "create new" (`POST /api/contacts` then link) (depends on T008, T039)
- [ ] T041 [P] [US4] Wire `LinkContactDialog` as the Contacts tab's "add contact" action in
  `ChildContactsTab.tsx` (depends on T039, T040)

**Checkpoint**: Directors can fully manage a child's linked family contacts from the web —
independently testable/demoable.

---

## Phase 6: User Story 3 - One combined invoice per family (Priority: P3)

**Goal**: An opt-in per-location toggle groups same-primary-contact siblings' invoices into one
PDF/parent-app entry/payment action, without altering the underlying per-child `Invoice` rows.

**Independent Test**: Enable bundling on a location, generate invoices for a 2-child family,
confirm one combined PDF/parent-app entry with both children's lines and one total; confirm
marking it paid marks both underlying invoices paid.

### Tests for User Story 3

- [X] T042 [P] [US3] Test: bundling enabled, 2 siblings same primary contact/location → both
  generated invoices share the same new `FamilyGroupId`; bundling disabled (default) → both
  `FamilyGroupId` remain null, in the same
  `backend/ChildCare.Api.Tests/Invoices/SiblingDiscountAndBundlingTests.cs` from Phase 4
- [X] T043 [P] [US3] Test: `GenerateFamilyInvoicePdfQuery` for a valid `FamilyGroupId` returns one
  PDF reflecting both children's line items and the combined total; an unrelated caller's
  `familyGroupId` guess (no linked child in the group) is indistinguishable from not-found, in
  `backend/ChildCare.Api.Tests/Invoices/FamilyInvoicePdfTests.cs`
- [X] T044 [P] [US3] Test: `MarkInvoicePaidCommand` on one invoice of a `FamilyGroupId` group
  transitions every invoice in the group `Sent → Paid` together, in
  `backend/ChildCare.Api.Tests/Invoices/InvoiceLifecycleTests.cs`
- [X] T045 [P] [US3] Test: `GetParentInvoicesQuery` collapses a family group into one
  `FamilyInvoiceResponse`-shaped entry; a non-grouped invoice remains a normal single entry, in
  `backend/ChildCare.Api.Tests/Invoices/GetParentInvoicesTests.cs`
- [X] T046 [P] [US3] Test: siblings with different primary contacts (Clarifications edge case) are
  never grouped together even when a secondary contact is shared, in
  `SiblingDiscountAndBundlingTests.cs`

### Implementation for User Story 3

- [X] T047 [US3] Extend `GenerateInvoicesCommand`'s grouping (T030) so that when
  `FamilyInvoiceBundlingEnabled` is true for the location, every invoice in a same-primary-
  contact group for the period receives one shared, newly generated `FamilyGroupId` (stable
  across regeneration of the same still-open group) per research.md R4 (depends on T010, T030)
- [X] T048 [US3] Implement `GenerateFamilyInvoicePdfQuery` + `QuestPdfFamilyInvoiceGenerator`
  (per-child section reusing each invoice's `InvoiceLineItems`, one combined total, mirrors
  `QuestPdfInvoiceGenerator`'s locale-label pattern) per research.md R5 in
  `backend/ChildCare.Application/Invoices/GenerateFamilyInvoicePdfQuery.cs` and
  `backend/ChildCare.Infrastructure/Pdf/QuestPdfFamilyInvoiceGenerator.cs`
- [X] T049 [US3] Extend `MarkInvoicePaidCommand`: when the target invoice has a `FamilyGroupId`,
  load and transition every sibling invoice sharing it to `Paid` in the same transaction, per
  research.md R5 in `backend/ChildCare.Application/Invoices/MarkInvoicePaidCommand.cs` (depends
  on T047)
- [X] T050 [US3] Extend `GetParentInvoicesQuery`'s response mapping to collapse invoices sharing a
  `FamilyGroupId` into one `FamilyInvoiceResponse` entry, per contracts/family-siblings-api.md in
  `backend/ChildCare.Application/Invoices/GetParentInvoicesQuery.cs` (depends on T003, T047)
- [X] T051 [US3] Add `GET /api/parent/invoices/family/{familyGroupId}/pdf` route (same
  indistinguishable-not-found authorization pattern as the existing per-invoice PDF route) in
  `backend/ChildCare.Api/Endpoints/InvoiceEndpoints.cs` (depends on T048)
- [ ] T052 [P] [US3] Add family invoice bundling toggle to
  `web/components/InvoiceSettingsForm.tsx` alongside the T033 discount field
- [ ] T053 [P] [US3] Render grouped family invoice entries (combined total, per-child lines,
  single download action) in `parent-mobile/app/(app)/invoices/index.tsx` (depends on T006)
- [ ] T054 [P] [US3] Component test: invoice list renders a `FamilyInvoiceResponse` entry
  distinctly from a normal single-invoice entry, in
  `parent-mobile/__tests__/invoices.test.tsx` (create if it doesn't already exist, mirroring
  existing parent-mobile test conventions)

**Checkpoint**: Family invoice bundling works end to end, independently testable/demoable, and
014/018's per-child invoice behavior is unchanged when bundling is off.

---

## Phase 7: User Story 5 - A departed sibling doesn't disappear without a trace (Priority: P3)

**Goal**: A parent-facing "previous children" view surfaces deactivated siblings and their
historical (read-only) data, without cluttering the default active-children view.

**Independent Test**: Deactivate one of two siblings; confirm the default view shows only the
active child, and the previous-children view surfaces the deactivated one with working
historical access.

### Tests for User Story 5

- [X] T055 [P] [US5] Test: `GetParentPreviousChildrenQuery` returns only the caller's deactivated
  linked children with correct enrollment-period dates; a caller with none gets an empty list,
  in `backend/ChildCare.Api.Tests/Parent/ParentPreviousChildrenTests.cs`
- [X] T056 [P] [US5] Test: `GetParentDailySummaryQuery`/`GetParentInvoicesQuery` already succeed
  for a deactivated child the caller is linked to (regression-confirming, no new authorization
  gap introduced) — extend `ParentDailySummaryTests.cs`/`GetParentInvoicesTests.cs` with a
  deactivated-child case

### Implementation for User Story 5

- [X] T057 [US5] Implement `GetParentPreviousChildrenQuery` (mirrors `GetParentChildrenQuery`,
  filters `DeactivatedAt != null`, adds enrollment-period dates) per research.md R8 in
  `backend/ChildCare.Application/Parent/GetParentPreviousChildrenQuery.cs` (depends on T004)
- [X] T058 [US5] Add `GET /api/parent/children/previous` route in
  `backend/ChildCare.Api/Endpoints/ParentEndpoints.cs` (depends on T057)
- [ ] T059 [US5] Create `parent-mobile/app/(app)/children/previous.tsx` — lists deactivated
  children (name, photo, enrollment period), links into the existing per-child daily-
  summary/invoices/milestones screens read-only (hides action buttons per FR-016) (depends on
  T006)
- [ ] T060 [US5] Add a "previous children" entry point to `parent-mobile/app/(app)/index.tsx`,
  shown only when `GET /api/parent/children/previous` returns a non-empty list (FR-017) (depends
  on T059)
- [ ] T061 [P] [US5] Component test: entry point hidden with zero deactivated children, shown and
  navigable with one, in `parent-mobile/__tests__/home.test.tsx`

**Checkpoint**: All five user stories complete — the full 030 scope is independently verifiable
via quickstart.md's five scenarios.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Cross-story validation and the explicit no-regression guarantee for 006/013/013a/
014/014a/018.

- [ ] T062 [P] Accessibility pass: bulk-reservation toggle, sibling-billing settings fields,
  Contacts tab actions, and the previous-children list meet this codebase's existing
  accessibility baseline (48pt touch targets on parent-mobile, focus rings on web —
  design-system.md/platform-rules.md)
- [ ] T063 Run quickstart.md's five scenarios manually/via integration tests and confirm each
  passes
- [ ] T064 Confirm SC-005 explicitly: run 014/014a's own existing test suites unmodified and
  confirm they still pass in full — a location that never configures sibling
  discount/bundling must see zero behavior change to invoice generation, sending, or manual
  mark-paid
- [ ] T065 Confirm 018's management-reporting queries (which key off `Invoice`/`ChildId`)
  produce identical output whether or not `FamilyGroupId` is set — no report regression from the
  new column

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: No Setup dependency (T009-T014 touch domain/infra files, not
  contracts) — can run in parallel with Setup. BLOCKS all user stories.
- **User Stories (Phase 3-7)**: All depend on Foundational (Phase 2) completion.
  - US1 (P1) depends only on Foundational — fully independent of every other story.
  - US2 (P2, sibling discount) depends only on Foundational — independent of US1/US3/US4/US5,
    though it and US3 both edit `GenerateInvoicesCommand` (T030/T047 are sequential, not
    parallel, to avoid the same-file conflict).
  - US4 (P2, web contacts) depends only on Foundational (T011's enum extension) — fully
    independent of every other story.
  - US3 (P3, bundling) depends on US2's grouping-by-primary-contact logic in
    `GenerateInvoicesCommand` (T030) being in place first — sequenced after US2 for that reason,
    not because bundling requires a discount to be configured.
  - US5 (P3, previous children) depends only on Foundational — fully independent of every other
    story.
- **Polish (Phase 8)**: Depends on all five user stories being complete.

### Parallel Opportunities

- T001-T008 (Setup) can all run in parallel.
- T009, T010, T011 (Foundational entity/enum extensions) can run in parallel.
- T015-T018 (US1 tests) can run in parallel.
- T022-T024 (US1 mobile work) can run in parallel once T019-T021 land.
- T025-T029 (US2 tests) can run in parallel.
- T035-T037 (US4 tests) can run in parallel.
- T042-T046 (US3 tests) can run in parallel.
- T052-T054 (US3 UI work) can run in parallel once T047-T051 land.
- T055-T056 (US5 tests) can run in parallel.
- US2 and US4 can be implemented in parallel by different work sessions (disjoint files); US3
  should follow US2 (see Phase Dependencies).

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: run quickstart.md Scenario 1
5. Demo if ready — the single highest-volume friction point (repeated absence forms) is solved

### Incremental Delivery

1. Setup + Foundational → schema ready
2. Add User Story 1 → validate independently (Scenario 1) → demo
3. Add User Story 2 → validate independently (Scenario 2) → demo (discount applies automatically)
4. Add User Story 4 → validate independently (Scenario 4) → demo (directors can finally see/
   manage family contacts)
5. Add User Story 3 → validate independently (Scenario 3) → demo (one combined invoice per
   family)
6. Add User Story 5 → validate independently (Scenario 5) → demo (departed siblings stay
   reachable)
7. Polish (Phase 8) → run all five quickstart.md scenarios end to end, including the explicit
   014/014a/018 no-regression checks (T064/T065)

---

## Notes

- [P] tasks touch different files, or the same file in a way that doesn't conflict with other
  in-flight [P] tasks in the same phase.
- [Story] label maps each task to its user story for traceability.
- T030/T047 intentionally share one file (`GenerateInvoicesCommand.cs`) across US2/US3 — see
  Phase Dependencies for why they're sequenced rather than parallel.
- T064/T065 exist specifically because "zero behavior change unless a director opts in"
  (SC-005) is the highest-risk regression surface in this feature (it touches the same invoice-
  generation path 014/014a/018 all depend on) — only running the *existing* suites unmodified,
  plus an explicit reporting-output check, actually proves it held.
- Commit after each task or logical group, per this repo's standing convention.
