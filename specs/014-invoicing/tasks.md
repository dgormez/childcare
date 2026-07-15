# Tasks: Invoicing

**Input**: Design documents from `specs/014-invoicing/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required by constitution Principle V (real PostgreSQL via TestContainers for backend
integration tests; component tests for web/parent-mobile), same standard every prior feature has
followed.

**Organization**: Tasks are grouped by user story to enable independent implementation and
testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Contracts DTOs and i18n scaffolding shared across all stories.

- [ ] T001 [P] Add `InvoiceResponse` (per contracts/invoicing-api.md's response shape) and request
  DTOs (`GenerateInvoicesRequest`, `UpdateInvoiceExtraChargesRequest`, `SendInvoicesRequest`,
  `MarkInvoicePaidRequest`) in `backend/ChildCare.Contracts/Responses/InvoiceResponse.cs` and
  `backend/ChildCare.Contracts/Requests/InvoiceRequests.cs`
- [ ] T002 [P] Add `UpdateLocationInvoiceSettingsRequest` and extend `LocationResponse` with
  `erkenningsnummer`/`bankAccountNumber`/`invoiceDueDays` in
  `backend/ChildCare.Contracts/Requests/LocationRequests.cs` and
  `backend/ChildCare.Contracts/Responses/LocationResponse.cs`
- [ ] T003 [P] Add `UpdateOrganisationRequest` (`kboNumber`) and extend the organisation-profile
  response with `kboNumber` in `backend/ChildCare.Contracts/Requests/OrganisationRequests.cs`
  and the response type `GetCurrentOrganisationQuery` returns
- [ ] T004 [P] Add director-web `invoices.*` i18n keys (list columns, status labels in plain
  language, generate/send/mark-paid/regenerate actions, extra-charge editor, empty/error states)
  to `web/i18n/locales/en.json`, `web/i18n/locales/fr.json`, `web/i18n/locales/nl.json`
- [ ] T005 [P] Add director-web `locations.invoiceSettings.*` i18n keys (erkenningsnummer, bank
  account number, invoice due days, save action) to the same three locale files
- [ ] T006 [P] Add director-web `organisationSettings.*` i18n keys (KBO number field + save) to
  the same three locale files
- [ ] T007 [P] Add parent-mobile `invoices.*` i18n keys (list, detail, plain-language statuses,
  download action, empty/error states) to `parent-mobile/i18n/locales/en.json`,
  `parent-mobile/i18n/locales/fr.json`, `parent-mobile/i18n/locales/nl.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The schema, entity, EF configuration, and shared money-correctness logic every user
story depends on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T008 Add `Invoice` entity + `InvoiceStatus` enum (`Draft`/`Sent`/`Paid`) in
  `backend/ChildCare.Domain/Entities/Invoice.cs` and `backend/ChildCare.Domain/Enums/InvoiceStatus.cs`
  per data-model.md
- [ ] T009 [P] Add `Erkenningsnummer`/`BankAccountNumber`/`InvoiceDueDays` (default 14) to
  `backend/ChildCare.Domain/Entities/Location.cs`
- [ ] T010 [P] Add `KboNumber` to `backend/ChildCare.Domain/Entities/Tenant.cs`
- [ ] T011 Configure `Invoice` in `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`
  — `LineItems` as `jsonb` (System.Text.Json conversion, mirrors `ChildEvent`'s existing JSONB
  pattern), `SequenceNumber` as an identity column (`ValueGeneratedOnAdd`, research.md R3), unique
  index on `(ChildId, ContractId, LocationId, PeriodMonth)`, and the new `Location` columns
  (depends on T008, T009)
- [ ] T012 Configure `Tenant.KboNumber` in `backend/ChildCare.Infrastructure/Persistence/PublicDbContext.cs`
  (depends on T010)
- [ ] T013 Add tenant migration `AddInvoices` (new `invoices` table, new `Location` columns) in
  `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` (depends on T011)
- [ ] T014 Add public migration `AddTenantKboNumber` in
  `backend/ChildCare.Infrastructure/Persistence/Migrations/Public/` (depends on T012)
- [ ] T015 [P] Implement `BillableDayCalculator` per data-model.md's algorithm in
  `backend/ChildCare.Application/Invoices/BillableDayCalculator.cs` (depends on T008)
- [ ] T016 [P] Implement `OgmReferenceGenerator` (modulo-97 checksum, `0`→`97` special case) in
  `backend/ChildCare.Application/Invoices/OgmReferenceGenerator.cs`
- [ ] T017 [P] Implement `InvoiceMapper.ToResponse` (including computed `isOverdue`) in
  `backend/ChildCare.Application/Invoices/InvoiceMapper.cs` (depends on T008)
- [ ] T018 Extend `TenantMigrationRolloutTests`' schema-revert helper for the new `invoices` table
  and `Location` columns (the recurring pattern every migration-adding feature since 003 has
  needed) in `backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs` (depends on T013)

**Checkpoint**: Schema, mapping, and shared money-correctness logic ready — user story
implementation can now begin.

---

## Phase 3: User Story 1 - Director generates a month's invoices for a location (Priority: P1) 🎯 MVP

**Goal**: A director can bulk-generate draft invoices for a location/month, each with a correct
billable-day breakdown; generation is idempotent and split-location contracts produce
independent invoices.

**Independent Test**: `POST /api/locations/{id}/invoices/generate` for a location with a mix of
present/unjustified-absent/closure days and mid-month contract boundaries; confirm each created
invoice's line items match the billable-day rule exactly, and confirm calling it again doesn't
duplicate anything.

### Tests for User Story 1

- [ ] T019 [P] [US1] Integration test: `BillableDayCalculator` computes present/unjustified/
  closure counts correctly against a range of `AttendanceRecord` fixtures, including a
  contracted day with no `AttendanceRecord` row at all (not billed, research.md R2) and mid-month
  contract start/end restricting the range, in `backend/ChildCare.Api.Tests/Invoices/BillableDayCalculatorTests.cs`
- [ ] T020 [P] [US1] Integration test: `OgmReferenceGenerator`/generated invoices produce a
  unique, checksum-valid reference for every invoice, including the `remainder == 0` → `97`
  special case, in `backend/ChildCare.Api.Tests/Invoices/OgmReferenceGeneratorTests.cs`
- [ ] T021 [P] [US1] Integration test: generating invoices for a location/month creates exactly
  one draft invoice per child with a contract active that month, with the correct computed
  breakdown and total (FR-001/FR-002, US1/AC1) in `backend/ChildCare.Api.Tests/Invoices/GenerateInvoicesTests.cs`
- [ ] T022 [P] [US1] Integration test: a contract that started or ended mid-month restricts the
  invoice to only the active portion (US1/AC2, AC3) in the same file
- [ ] T023 [P] [US1] Integration test: a child with active contracts at two locations gets two
  independent invoices, one per location, when generated separately (FR-014, US1/AC4) in the
  same file
- [ ] T024 [P] [US1] Integration test: generating twice for the same location/month does not
  duplicate invoices — the existing draft is recomputed in place (FR-003, US1/AC5) in the same
  file
- [ ] T025 [P] [US1] Integration test: a contract that exists for the month but was never active
  (e.g. cancelled before its start date) produces no invoice (Edge Cases) in the same file

### Implementation for User Story 1

- [ ] T026 [US1] Implement `GenerateInvoicesCommand` (bulk, per location/month; idempotent
  recompute of existing drafts) in `backend/ChildCare.Application/Invoices/GenerateInvoicesCommand.cs`
  (depends on T015, T016, T017)
- [ ] T027 [US1] Implement `ListInvoicesQuery` and `GetInvoiceByIdQuery` (director) in
  `backend/ChildCare.Application/Invoices/ListInvoicesQuery.cs` and `GetInvoiceByIdQuery.cs`
  (depends on T017)
- [ ] T028 [US1] Wire `POST /api/locations/{locationId}/invoices/generate`,
  `GET /api/locations/{locationId}/invoices`, and `GET /api/invoices/{id}` in
  `backend/ChildCare.Api/Endpoints/InvoiceEndpoints.cs` (depends on T026, T027)
- [ ] T029 [US1] Implement `UpdateLocationInvoiceSettingsCommand` (mirrors
  `UpdateLocationReservationSettingsCommand`, 013f) + wire
  `PUT /api/locations/{id}/invoice-settings` in
  `backend/ChildCare.Application/Locations/UpdateLocationInvoiceSettingsCommand.cs` and
  `backend/ChildCare.Api/Endpoints/LocationEndpoints.cs` (depends on T009, T011)
- [ ] T030 [US1] Implement `UpdateOrganisationCommand` (`KboNumber`) + wire
  `PUT /api/organisations/me` in `backend/ChildCare.Application/Organisations/UpdateOrganisationCommand.cs`
  and `backend/ChildCare.Api/Endpoints/OrganisationEndpoints.cs` (depends on T010, T012)
- [ ] T031 [P] [US1] Integration test: `PUT .../invoice-settings` persists erkenningsnummer/bank
  account number/invoice-due-days and leaves other locations unchanged; non-`DirectorOnly`
  callers rejected in `backend/ChildCare.Api.Tests/Invoices/LocationInvoiceSettingsTests.cs`
- [ ] T032 [P] [US1] Integration test: `PUT /api/organisations/me` persists `kboNumber`; non-
  `DirectorOnly` callers rejected in `backend/ChildCare.Api.Tests/Invoices/OrganisationSettingsTests.cs`
- [ ] T033 [US1] Regenerate and commit `web/lib/generated/api-types.ts` against the new endpoints
- [ ] T034 [US1] Create `InvoiceSettingsForm.tsx` (mirrors `ReservationSettingsForm.tsx`) in
  `web/components/InvoiceSettingsForm.tsx` (depends on T033)
- [ ] T035 [US1] Add an "Invoicing" tab to the location detail page in
  `web/app/(app)/locations/[id]/page.tsx` (depends on T034)
- [ ] T036 [US1] Create a minimal organisation-profile settings page (KBO number field, save
  action — no existing org-settings screen exists in `web/` yet, so this is a new, deliberately
  small page, not a full settings dashboard) in `web/app/(app)/settings/page.tsx` (depends on
  T033)
- [ ] T037 [US1] Create `web/components/invoices/InvoiceTable.tsx` (filterable/sortable list —
  status, month — high-density table per `platform-rules.md`'s Director Web section) and
  `web/app/(app)/invoices/page.tsx` (location/month picker + "Generate invoices" action + the
  table) (depends on T033)
- [ ] T038 [P] [US1] Web component test: the invoice settings tab loads/saves erkenningsnummer/
  bank account/due-days in `web/__tests__/invoiceSettings.test.tsx`
- [ ] T039 [P] [US1] Web component test: the organisation settings page loads/saves the KBO
  number in `web/__tests__/organisationSettings.test.tsx`
- [ ] T040 [P] [US1] Web component test: selecting a location/month and clicking "Generate
  invoices" renders the resulting table rows with correct amounts/statuses in
  `web/__tests__/invoiceGeneration.test.tsx`

**Checkpoint**: A director can configure invoicing settings and generate a location's invoices
end to end, independently of sending/payment tracking or the parent-facing view.

---

## Phase 4: User Story 2 - Director reviews, sends, and tracks payment (Priority: P1)

**Goal**: A director can add extra charges to a draft, send one or many invoices, and record
payment; a `sent` invoice past its due date reads as overdue automatically.

**Independent Test**: Add an extra charge to a draft invoice, send it, confirm the total includes
the charge and the invoice is now parent-visible with a PDF containing every required field; mark
it paid and confirm the status updates.

### Tests for User Story 2

- [ ] T041 [P] [US2] Integration test: `PUT .../extra-charges` on a draft invoice updates
  `lineItems.extraCharges` and recomputes `totalCents`; rejected (`422`) on a non-draft invoice
  (FR-006, US2/AC1) in `backend/ChildCare.Api.Tests/Invoices/InvoiceLifecycleTests.cs`
- [ ] T042 [P] [US2] Integration test: `POST /api/invoices/send` transitions one or many draft
  invoices to `sent`, sets `sentAt`/`dueDate`, generates the OGM reference if not already
  present, and notifies each parent; rejects the whole batch (no partial send) if any id isn't
  currently `draft` (FR-007, FR-013, US2/AC2, AC3) in the same file
- [ ] T043 [P] [US2] Integration test: a `sent` invoice whose `dueDate` has passed with no
  payment reads as `isOverdue: true` in list/detail responses, with no stored status change
  (FR-010, US2/AC4) in the same file
- [ ] T044 [P] [US2] Integration test: `POST /api/invoices/{id}/mark-paid` on a `sent` (or
  overdue) invoice sets `status: paid` and `paidAt`, and is rejected (`422`) on a `draft` invoice
  (FR-009, FR-013, US2/AC5, Edge Cases) in the same file
- [ ] T045 [P] [US2] Integration test: `GET /api/invoices/{id}/pdf` renders a PDF containing the
  KDV name/address/KBO/erkenningsnummer (if set), parent/child name, period, line-item
  breakdown, total, due date, OGM reference, and bank account number (if set) (FR-005, US3/AC4 —
  shared PDF-content assertion reused by the parent-facing test too) in
  `backend/ChildCare.Api.Tests/Invoices/InvoicePdfTests.cs`

### Implementation for User Story 2

- [ ] T046 [US2] Implement `UpdateInvoiceExtraChargesCommand` in
  `backend/ChildCare.Application/Invoices/UpdateInvoiceExtraChargesCommand.cs` (depends on T017)
- [ ] T047 [US2] Implement `SendInvoicesCommand` (batch, all-or-nothing; computes `DueDate` from
  the location's `InvoiceDueDays`, generates the OGM reference, sends the existing
  email/push notification per child's linked parent(s)) in
  `backend/ChildCare.Application/Invoices/SendInvoicesCommand.cs` (depends on T016, T017)
- [ ] T048 [US2] Implement `MarkInvoicePaidCommand` in
  `backend/ChildCare.Application/Invoices/MarkInvoicePaidCommand.cs` (depends on T017)
- [ ] T049 [US2] Implement `QuestPdfInvoiceGenerator`/`IInvoicePdfGenerator` (mirrors
  `QuestPdfContractGenerator`'s per-locale `Labels` dictionary pattern exactly) in
  `backend/ChildCare.Application/Common/IInvoicePdfGenerator.cs` and
  `backend/ChildCare.Infrastructure/Pdf/QuestPdfInvoiceGenerator.cs`, and
  `GenerateInvoicePdfQuery` (mirrors `GenerateContractPdfQuery` — on-demand render, research.md
  R1) in `backend/ChildCare.Application/Invoices/GenerateInvoicePdfQuery.cs`
- [ ] T050 [US2] Wire `PUT /api/invoices/{id}/extra-charges`, `POST /api/invoices/send`,
  `POST /api/invoices/{id}/mark-paid`, and `GET /api/invoices/{id}/pdf` in
  `backend/ChildCare.Api/Endpoints/InvoiceEndpoints.cs` (depends on T046, T047, T048, T049)
- [ ] T051 [US2] Create `web/components/invoices/InvoiceDetail.tsx` (breakdown, extra-charge
  editor, send/mark-paid actions, PDF download link) and
  `web/app/(app)/invoices/[id]/page.tsx` (depends on T033)
- [ ] T052 [P] [US2] Web component test: adding an extra charge and sending updates the total and
  status; the mark-paid action updates status/paid date in `web/__tests__/invoiceDetail.test.tsx`

**Checkpoint**: A director can fully review, send, and track payment for any generated invoice,
including correct overdue display, independently of the parent-facing view or regeneration.

---

## Phase 5: User Story 3 - Parent views and downloads their invoices (Priority: P1)

**Goal**: A parent sees only their own children's `sent`/`paid`/overdue invoices (never a draft),
each correctly attributed to the right child, and can download the PDF.

**Independent Test**: With a `sent` invoice for a parent's child, confirm it appears in the
parent's invoice list with the correct amount/status and that the PDF download contains the OGM
reference and every other required field.

### Tests for User Story 3

- [ ] T053 [P] [US3] Integration test: `GET /api/parent/invoices` returns only `sent`/`paid`
  invoices (never `draft`) for the requesting parent's own children, correctly attributed to
  each child (FR-008, US3/AC1, AC3) in `backend/ChildCare.Api.Tests/Invoices/GetParentInvoicesTests.cs`
- [ ] T054 [P] [US3] Integration test: a parent with two children (same or different locations)
  sees every invoice for every child, not just one (US3/AC2) in the same file
- [ ] T055 [P] [US3] Integration test: `GET /api/parent/invoices/{id}/pdf` is rejected (`404`)
  for an invoice that doesn't belong to one of the requesting parent's children, or is still
  `draft` (Security considerations) in the same file

### Implementation for User Story 3

- [ ] T056 [US3] Implement `GetParentInvoicesQuery` (mirrors `GetParentMonthlyMenuQuery`'s
  parent-contact-resolution + per-child-entry pattern, 013j) in
  `backend/ChildCare.Application/Invoices/GetParentInvoicesQuery.cs` (depends on T017)
- [ ] T057 [US3] Wire `GET /api/parent/invoices` and `GET /api/parent/invoices/{id}/pdf` in
  `backend/ChildCare.Api/Endpoints/InvoiceEndpoints.cs` (depends on T056, T049)
- [ ] T058 [US3] Create `parent-mobile/services/invoices.ts` (fetch pattern, mirrors
  `services/menu.ts`) in `parent-mobile/services/invoices.ts`
- [ ] T059 [US3] Create `parent-mobile/app/(app)/invoices/index.tsx` (list, plain-language
  status) and `parent-mobile/app/(app)/invoices/[id].tsx` (detail + PDF download) (depends on
  T058)
- [ ] T060 [P] [US3] Parent-mobile component test: the invoice list shows only sent/paid
  invoices for the parent's own children, correctly attributed, with plain-language status in
  `parent-mobile/__tests__/invoices.test.tsx`

**Checkpoint**: All three P1 user stories are independently functional — a director can
configure, generate, send, and track payment, and a parent can see and download exactly their
own children's invoices.

---

## Phase 6: User Story 4 - Director regenerates an invoice after correcting attendance (Priority: P2)

**Goal**: A director can regenerate a `draft` or `sent` invoice's line items/total after an
attendance correction; a `paid` invoice is immutable; regenerating a `sent` invoice re-notifies
the parent.

**Independent Test**: Correct an attendance record underlying a `sent` invoice, regenerate it,
confirm the line items/total change while the OGM reference stays the same, and confirm a `paid`
invoice rejects the same attempt.

### Tests for User Story 4

- [ ] T061 [P] [US4] Integration test: regenerating a `draft` invoice recomputes line items with
  no parent notification (US4/AC1) in `backend/ChildCare.Api.Tests/Invoices/RegenerateInvoiceTests.cs`
- [ ] T062 [P] [US4] Integration test: regenerating a `sent` invoice recomputes line items/total,
  keeps the same `ogmReference`, and re-notifies the parent (FR-011, US4/AC2) in the same file
- [ ] T063 [P] [US4] Integration test: attempting to regenerate a `paid` invoice is rejected
  (`422 errors.invoice.paid_immutable`) and the invoice is byte-for-byte unchanged (FR-012,
  US4/AC3, SC-005) in the same file

### Implementation for User Story 4

- [ ] T064 [US4] Implement `RegenerateInvoiceCommand` in
  `backend/ChildCare.Application/Invoices/RegenerateInvoiceCommand.cs` (depends on T015, T017,
  T047)
- [ ] T065 [US4] Wire `POST /api/invoices/{id}/regenerate` in
  `backend/ChildCare.Api/Endpoints/InvoiceEndpoints.cs` (depends on T064)
- [ ] T066 [US4] Add a "Regenerate" action to `web/components/invoices/InvoiceDetail.tsx`,
  disabled/hidden for a `paid` invoice (depends on T051, T065)
- [ ] T067 [P] [US4] Web component test: the regenerate action is available on draft/sent
  invoices and absent/disabled on a paid invoice in `web/__tests__/invoiceDetail.test.tsx`

**Checkpoint**: All four user stories are independently functional end to end.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that span all stories.

- [ ] T068 [P] Accessibility pass: invoice table, detail actions, and settings forms fully
  keyboard-operable, focus-visible, per `platform-rules.md`'s Director Web section, in
  `web/components/invoices/InvoiceTable.tsx`, `InvoiceDetail.tsx`, `InvoiceSettingsForm.tsx`
- [ ] T069 Run `quickstart.md`'s four scenarios manually/via integration tests and confirm each
  expected outcome
- [ ] T070 Confirm SC-004 explicitly: run 007/009/010/011/013a's own existing test suites
  unmodified against the new schema and confirm 100% still pass with zero changes needed — the
  strongest possible evidence invoicing only reads existing data and never writes to it

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup only for T004-T007's i18n keys being available to
  reference; T008-T018 have no Setup dependency and can start immediately. BLOCKS all user
  stories.
- **User Stories (Phase 3-6)**: All depend on Foundational (Phase 2) completion.
  - US1 (P1) has no dependency on US2/US3/US4 — generation and settings can exist before any
    sending/payment tracking or parent-facing view.
  - US2 (P1) depends on US1's generated invoices existing to send/track, but its own
    endpoint/UI work (T046-T052) is otherwise independent.
  - US3 (P1) depends on US2's send path (a `sent` invoice must exist to be parent-visible) and
    on `IInvoicePdfGenerator`/`GenerateInvoicePdfQuery` (T049, built in US2) for its own PDF
    route, but its own query/UI work (T056-T060) is otherwise independent.
  - US4 (P2) depends on US1's generation and US2's send path existing (there must be a draft or
    sent invoice to regenerate), but its own command/UI work (T064-T067) is otherwise
    independent.
- **Polish (Phase 7)**: Depends on all four user stories being complete.

### Parallel Opportunities

- T001-T007 (Setup) can all run in parallel.
- T009 and T010 (Foundational entity extensions) can run in parallel.
- T015, T016, T017 (Foundational money-correctness logic) can run in parallel once T008 lands.
- T019-T025 (US1 tests) can run in parallel.
- T031 and T032 (US1 settings tests) can run in parallel.
- T038, T039, T040 (US1 web tests) can run in parallel once T034-T037 land.
- T041-T045 (US2 tests) can run in parallel.
- T053-T055 (US3 tests) can run in parallel.
- T061-T063 (US4 tests) can run in parallel.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: run quickstart.md Scenario 1 independently
5. Demo if ready — a director can already see correctly computed draft invoices, even with no
   send/payment-tracking or parent-facing UI yet

### Incremental Delivery

1. Setup + Foundational → schema and shared money-correctness logic ready
2. Add User Story 1 → validate independently → demo (generation + settings only)
3. Add User Story 2 → validate independently (Scenario 2 in full) → demo (send/pay works)
4. Add User Story 3 → validate independently (Scenario 2's parent-facing steps) → demo (parents
   see it — the P1 slice is now feature-complete)
5. Add User Story 4 → validate independently (Scenario 3's regeneration half) → demo (attendance
   corrections propagate correctly)
6. Polish (Phase 7) → run all four `quickstart.md` scenarios end to end, including the explicit
   read-only regression check (T070)

---

## Notes

- [P] tasks touch different files, or the same file in a way that doesn't conflict with other
  in-flight [P] tasks in the same phase.
- [Story] label maps each task to its user story for traceability.
- T070 exists specifically because SC-004's "zero behavior change to any other existing feature"
  guarantee is the highest-risk regression surface in this feature (invoicing reads
  `AttendanceRecord`/`Contract` but must never write to them) — only running the *existing* test
  suites unmodified actually proves it held.
- Commit after each task or logical group, per this repo's standing convention.
