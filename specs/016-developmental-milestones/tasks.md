# Tasks: Developmental Milestones

**Input**: Design documents from `specs/016-developmental-milestones/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required by constitution Principle V (real PostgreSQL via TestContainers for backend
integration tests; component tests for mobile/web/parent-mobile), same standard every prior
feature has followed.

**Organization**: Tasks are grouped by user story to enable independent implementation and
testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Contracts DTOs and i18n scaffolding shared across all stories.

- [X] T001 [P] Add `DevelopmentalDomainResponse` (id, code, nameNl, nameFr, nameEn, sortOrder,
  milestones[]), `DevelopmentalMilestoneResponse` (id, ageFromMonths, ageToMonths, descriptionNl,
  descriptionFr, descriptionEn, sortOrder, currentStatus?, isCurrentFocus, history[]?), and
  `RecordMilestoneObservationRequest` (milestoneId, status, observedAt, notes?) per
  contracts/developmental-milestones-api.md in
  `backend/ChildCare.Contracts/Responses/DevelopmentalMilestoneResponses.cs` and
  `backend/ChildCare.Contracts/Requests/MilestoneObservationRequests.cs`
- [X] T002 [P] Add caregiver-app `milestones.*` i18n keys (entry point label, domain names
  fallback labels, status labels — emerging/achieved/not yet, save confirmation, empty state,
  offline-queued state) to `mobile/i18n/locales/en.json`, `mobile/i18n/locales/fr.json`,
  `mobile/i18n/locales/nl.json`
- [X] T003 [P] Add director-web `children.milestones.*` i18n keys (tab label, domain group
  headers, status labels + icons, current-focus badge, history panel, empty state, PDF export
  action) to `web/i18n/locales/en.json`, `web/i18n/locales/fr.json`, `web/i18n/locales/nl.json`
- [X] T004 [P] Add parent-mobile `milestones.*` i18n keys (warm, non-clinical section title and
  copy — e.g. "Development" section, current-focus framing, empty state "Nothing recorded yet —
  check back soon", PDF download action) to `parent-mobile/i18n/locales/en.json`,
  `parent-mobile/i18n/locales/fr.json`, `parent-mobile/i18n/locales/nl.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared catalog entities/seed, the tenant-scoped observation entity, and the
shared portfolio-building logic every user story depends on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 [P] Add `DevelopmentalDomain` entity (Id, Code, NameNl, NameFr, NameEn, SortOrder) in
  `backend/ChildCare.Domain/Entities/DevelopmentalDomain.cs` per data-model.md (public schema,
  research.md R1)
- [X] T006 [P] Add `DevelopmentalMilestone` entity (Id, DomainId, AgeFromMonths, AgeToMonths,
  DescriptionNl, DescriptionFr, DescriptionEn, SortOrder) in
  `backend/ChildCare.Domain/Entities/DevelopmentalMilestone.cs` per data-model.md (public schema)
- [X] T007 Add `ChildMilestoneObservation` entity (Id, ChildId, MilestoneId, Status, ObservedAt,
  ObservedBy, Notes, CreatedAt — no `UpdatedAt`, no soft-delete column, research.md R3) in
  `backend/ChildCare.Domain/Entities/ChildMilestoneObservation.cs` per data-model.md (tenant
  schema)
- [X] T008 Configure `DevelopmentalDomain`/`DevelopmentalMilestone` (unique index on `Code`;
  `(DomainId, SortOrder)` and `(AgeFromMonths, AgeToMonths)` indexes on milestones) in
  `backend/ChildCare.Infrastructure/Persistence/PublicDbContext.cs` (depends on T005, T006)
- [X] T009 Configure `ChildMilestoneObservation` (index on `(ChildId, MilestoneId, CreatedAt)` and
  `(ChildId)`, no FK on `MilestoneId` per research.md R1) in
  `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs` (depends on T007)
- [X] T010 Add public-schema migration `AddDevelopmentalMilestoneCatalog` — creates both catalog
  tables and seeds the 7 domains plus their milestones (research.md R7, standard Belgian
  developmental framework spanning 0–36 months) via `migrationBuilder.InsertData`, mirroring
  `AddVaccineTypeCatalog.cs`'s exact seeding approach, in
  `backend/ChildCare.Infrastructure/Persistence/Migrations/Public/` (depends on T008)
- [X] T011 Add tenant migration `AddChildMilestoneObservations` in
  `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` (depends on T009)
- [X] T012 Extend `TenantMigrationRolloutTests`' schema-revert helper for the new table (the
  recurring pattern every migration-adding feature since 003 has needed) in
  `backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs` (depends on T011)
- [X] T013 [P] Implement `MilestonePortfolioBuilder` (research.md R2 — the shared age-band
  resolution + domain-grouping logic: given a child's `DateOfBirth`, today's date, the full
  catalog, and the child's observations, produce domain-grouped milestones each with current
  status, `isCurrentFocus`, and full history; age bands inclusive both ends) in
  `backend/ChildCare.Application/DevelopmentalMilestones/MilestonePortfolioBuilder.cs` (depends on
  T005, T006, T007)
- [X] T014 [P] Define `IMilestonePortfolioPdfGenerator` port + `MilestonePortfolioPdfModel`
  (mirrors `IInvoicePdfGenerator`'s on-demand/unstored shape — child name, domain-grouped
  milestones with current status + current-focus flag, locale) in
  `backend/ChildCare.Application/Common/IMilestonePortfolioPdfGenerator.cs` (research.md R4)
- [X] T015 [P] Add `DevelopmentalMilestoneMapper` (catalog entity → response, `MilestonePortfolioBuilder`
  output → `DevelopmentalMilestoneResponse`) in
  `backend/ChildCare.Application/DevelopmentalMilestones/DevelopmentalMilestoneMapper.cs` (depends
  on T001)
- [X] T016 [P] Unit test: `MilestonePortfolioBuilder` resolves the correct age band inclusively at
  both boundaries (e.g. exactly 15 and exactly 21 months both match a 15–21 month band) and
  computes "current status" as the most recent observation, with no observations rendering as
  "no observations yet" rather than throwing, in
  `backend/ChildCare.Api.Tests/DevelopmentalMilestones/MilestonePortfolioBuilderTests.cs`
- [X] T017 [P] Integration test: the seeded catalog contains all 7 domains
  (`motor_gross`/`motor_fine`/`language`/`cognitive`/`social`/`emotional`/`self_care`) with
  milestones covering the 0–36 month range in NL/FR/EN, in
  `backend/ChildCare.Api.Tests/DevelopmentalMilestones/DevelopmentalMilestoneCatalogSeedTests.cs`

**Checkpoint**: Catalog + observation entities, migrations, and the shared portfolio builder are
ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Caregiver records a milestone observation (Priority: P1) 🎯 MVP

**Goal**: A caregiver records an observation for a child against a catalog milestone from the
tablet, and it is saved immutably — a later regression is a new row, not an edit.

**Independent Test**: Open a child's profile on the caregiver app, record an observation with a
given status, and confirm it is saved and appears in that milestone's history — independent of
the director or parent views.

### Tests for User Story 1

- [X] T018 [P] [US1] Integration test: `RecordMilestoneObservationCommand` persists a new
  observation with the correct `ObservedBy` derived from the device/shift claims (matches
  `child_events`' `recorded_by` derivation, research.md R6), in
  `backend/ChildCare.Api.Tests/DevelopmentalMilestones/RecordMilestoneObservationCommandTests.cs`
- [X] T019 [P] [US1] Integration test: recording a second observation for the same milestone (a
  regression, e.g. `achieved` then `not_yet`) creates a second row and leaves the first
  unmodified; the portfolio's current status reflects only the latest, in the same test file
- [X] T020 [P] [US1] Integration test: submitting a status outside
  `emerging`/`achieved`/`not_yet` is rejected by FluentValidation before it reaches the database
  (FR-012), in the same test file
- [X] T021 [P] [US1] Structural test: confirm no update or delete MediatR command/endpoint exists
  for `child_milestone_observations` (research.md R3) — grep-based assertion over
  `ChildCare.Application`/`ChildCare.Api` that no such command/route is defined, in
  `backend/ChildCare.Api.Tests/DevelopmentalMilestones/MilestoneObservationImmutabilityTests.cs`

### Implementation for User Story 1

- [X] T022 [US1] Implement `RecordMilestoneObservationCommand` + FluentValidation validator
  (status enum check, milestone-exists-in-catalog check) in
  `backend/ChildCare.Application/DevelopmentalMilestones/RecordMilestoneObservationCommand.cs`
  (depends on T007, T013)
- [X] T023 [US1] Implement `ListDevelopmentalMilestonesQuery` (reads `IPublicDbContext`, mirrors
  `ListVaccineTypesQuery`) in
  `backend/ChildCare.Application/DevelopmentalMilestones/ListDevelopmentalMilestonesQuery.cs`
  (depends on T015)
- [X] T024 [US1] Wire `GET /api/developmental-domains` (open to any authenticated caller) and
  `POST /api/children/{childId}/milestone-observations` (`DeviceOrStaffOrDirector`) in
  `backend/ChildCare.Api/Endpoints/DevelopmentalMilestoneEndpoints.cs` (depends on T022, T023)
- [X] T025 [US1] Register `DevelopmentalMilestoneEndpoints` mapping in
  `backend/ChildCare.Api/Program.cs`
- [X] T026 [US1] Regenerate and commit `mobile/services/generated/api-types.ts` against the new
  endpoints (`npm run generate-api-client` in `mobile/`)
- [X] T027 [US1] Create `mobile/services/milestones.ts` (fetch catalog, submit observation,
  integrates `services/offlineQueue` for offline-first writes — mirrors
  `services/childEvents.ts`'s exact pattern) (depends on T026)
- [X] T028 [US1] Create `mobile/components/milestones/MilestoneEntrySheet.tsx` (domain → milestone
  → status tap → optional note, mirrors `EditEventModal.tsx`'s structure and the 48pt
  touch-target/large-control conventions) (depends on T027)
- [X] T029 [US1] Create `mobile/components/milestones/MilestoneTimeline.tsx` (per-milestone
  history list, mirrors `EventTimeline.tsx`) and wire a "Milestones" entry point into
  `mobile/app/(app)/child/[id].tsx` (depends on T028)
- [X] T030 [P] [US1] Caregiver-app component test: recording an observation shows an immediate
  save confirmation and appears at the top of that milestone's history; recording while offline
  queues locally and shows the pending-sync state, in `mobile/__tests__/milestones.test.tsx`

**Checkpoint**: Caregivers can record observations end to end — User Story 1 is independently
demonstrable via the API/caregiver app.

---

## Phase 4: User Story 2 - Director views a child's milestone portfolio (Priority: P1)

**Goal**: A director opens a child's profile and sees the full, domain-grouped portfolio with the
age-appropriate band highlighted and full per-milestone history available.

**Independent Test**: Seed observations across domains for a child of a known age, open that
child's profile on web, and confirm the portfolio groups correctly with the age-appropriate band
highlighted — independent of how the parent view renders.

### Tests for User Story 2

- [X] T031 [P] [US2] Integration test: `GetChildMilestonePortfolioQuery` returns every domain with
  its milestones grouped and ordered, each showing current status and full history, and flags the
  correct band as `isCurrentFocus` for a child of a known age, in
  `backend/ChildCare.Api.Tests/DevelopmentalMilestones/GetChildMilestonePortfolioQueryTests.cs`
- [X] T032 [P] [US2] Integration test: a child with zero observations returns every catalog
  milestone with a "no observations yet" current status rather than an error or an empty payload,
  in the same test file

### Implementation for User Story 2

- [X] T033 [US2] Implement `GetChildMilestonePortfolioQuery` (tenant-scoped, calls
  `MilestonePortfolioBuilder`, includes full per-milestone history) in
  `backend/ChildCare.Application/DevelopmentalMilestones/GetChildMilestonePortfolioQuery.cs`
  (depends on T013, T015)
- [X] T034 [US2] Wire `GET /api/children/{childId}/milestone-portfolio` (`StaffOrDirector`) in
  `backend/ChildCare.Api/Endpoints/DevelopmentalMilestoneEndpoints.cs` (depends on T033)
- [X] T035 [US2] Regenerate and commit `web/lib/generated/api-types.ts` against the new endpoints
  (`npm run generate-api-client` in `web/`)
- [X] T036 [US2] Add a "Milestones" tab to `web/app/(app)/children/[id]/page.tsx` and create
  `web/components/milestones/MilestonePortfolioView.tsx` (domain groups, current-focus visual
  distinction, click-through to per-milestone history — mirrors `VaccineRecordForm.tsx`'s
  per-child section pattern; status shown with icon + color per design-system.md, never color
  alone) (depends on T035)
- [X] T037 [P] [US2] Web component test: the Milestones tab groups by domain, visually
  distinguishes the age-appropriate band from other bands, and shows a clear empty state for a
  child with no observations, in `web/__tests__/milestones.test.tsx`

**Checkpoint**: Directors can review any child's full portfolio — combined with User Story 1, the
record-then-review loop works end to end.

---

## Phase 5: User Story 3 - Parent views their child's shared milestone portfolio (Priority: P2)

**Goal**: A parent opens their child's Development section in the parent app and sees the same
domain-grouped, age-highlighted view, worded warmly, strictly scoped to their own linked child.

**Independent Test**: Seed observations for a child, open the parent app as that child's linked
contact, and confirm the same domain-grouped, age-highlighted view is visible — independent of
caregiver/director actions happening at the same time.

### Tests for User Story 3

- [X] T038 [P] [US3] Integration test: `GetParentMilestonePortfolioQuery` returns the
  domain-grouped portfolio (current status + `isCurrentFocus` per milestone, no per-observation
  history) for a linked contact, and returns `Forbidden` for a contact not linked to the child
  (mirrors `GetParentDailySummaryQuery`'s exact pattern), in
  `backend/ChildCare.Api.Tests/DevelopmentalMilestones/GetParentMilestonePortfolioQueryTests.cs`
- [X] T039 [P] [US3] Integration test: a child with zero observations yields an empty-but-successful
  response for the parent query (not an error), in the same test file

### Implementation for User Story 3

- [X] T040 [US3] Implement `GetParentMilestonePortfolioQuery` (resolves `Contact` via
  `ICurrentParentContactResolver`, checks `ChildContacts` ownership, delegates to
  `MilestonePortfolioBuilder`, strips per-observation history from the response) in
  `backend/ChildCare.Application/DevelopmentalMilestones/GetParentMilestonePortfolioQuery.cs`
  (depends on T013, T015)
- [X] T041 [US3] Wire `GET /api/parent/children/{childId}/milestone-portfolio` (`ParentOnly`) in
  `backend/ChildCare.Api/Endpoints/DevelopmentalMilestoneEndpoints.cs` (depends on T040)
- [X] T042 [US3] Regenerate and commit `parent-mobile/services/generated/api-types.ts` against the
  new endpoints (`npm run generate-api-client` in `parent-mobile/`)
- [X] T043 [US3] Create `parent-mobile/services/milestones.ts` (fetch portfolio) (depends on T042)
- [X] T044 [US3] Create a "Development" section/screen in `parent-mobile/app/(app)/children/[id]/`
  (or the equivalent existing per-child navigation entry point — exact placement confirmed
  against current parent-mobile nav during implementation), domain-grouped, warm plain-language
  copy, current-focus highlight, empty state ("Nothing recorded yet — check back soon") (depends
  on T043)
- [X] T045 [P] [US3] Parent-mobile component test: the Development section shows the same
  domain-grouped structure, warm empty state for no data, and is unreachable for a child the
  signed-in parent isn't linked to, in `parent-mobile/__tests__/milestones.test.tsx`

**Checkpoint**: All three core actors — caregiver, director, parent — can record/view a child's
portfolio end to end.

---

## Phase 6: User Story 4 - Portfolio PDF export (Priority: P3)

**Goal**: A director or parent can trigger an on-demand PDF snapshot of a child's current
milestone portfolio.

**Independent Test**: Trigger a PDF export for a child with existing observations and confirm the
rendered document matches the current in-app portfolio content.

### Tests for User Story 4

- [X] T046 [P] [US4] Integration test: the milestone-portfolio PDF endpoint (director and parent
  variants) returns a valid, non-empty PDF (`application/pdf` content-type, `%PDF` magic bytes —
  mirrors `InvoicePdfTests`' assertion shape) reflecting current observations, and still succeeds
  (showing the empty state text) for a child with none, in
  `backend/ChildCare.Api.Tests/DevelopmentalMilestones/MilestonePortfolioPdfTests.cs`
- [X] T047 [P] [US4] Integration test: a parent PDF request for a child they aren't linked to is
  rejected, matching the JSON-endpoint authorization behavior, in the same test file

### Implementation for User Story 4

- [X] T048 [US4] Implement `QuestPdfMilestonePortfolioGenerator` (per-locale `Labels` dictionary,
  mirrors `QuestPdfInvoiceGenerator`'s pattern; renders domain groups with current-focus visually
  distinguished) in
  `backend/ChildCare.Infrastructure/Pdf/QuestPdfMilestonePortfolioGenerator.cs` (depends on T014)
- [X] T049 [US4] Register `IMilestonePortfolioPdfGenerator` →
  `QuestPdfMilestonePortfolioGenerator` in `backend/ChildCare.Api/Program.cs` (depends on T048)
- [X] T050 [US4] Wire `GET /api/children/{childId}/milestone-portfolio/pdf` (`StaffOrDirector`)
  and `GET /api/parent/children/{childId}/milestone-portfolio/pdf` (`ParentOnly`, same ownership
  check as T040) in `backend/ChildCare.Api/Endpoints/DevelopmentalMilestoneEndpoints.cs` (depends
  on T033, T040, T049)
- [X] T051 [US4] Add a "Download PDF" action to
  `web/components/milestones/MilestonePortfolioView.tsx` (depends on T036, T050)
- [X] T052 [US4] Add a "Download PDF" action to the parent-mobile Development section, reusing the
  existing `expo-file-system`/`expo-sharing` download pattern already established for
  invoices/fiscal attestations (depends on T044, T050)
- [X] T053 [P] [US4] Web + parent-mobile component tests: the download action succeeds for a
  child with data and for a child with none (empty-state PDF, not a failure), in
  `web/__tests__/milestones.test.tsx` and `parent-mobile/__tests__/milestones.test.tsx`

**Checkpoint**: All four user stories complete — record, director review, parent review, and PDF
export are all demonstrable end to end.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Cross-story validation and accessibility.

- [X] T054 [P] Accessibility pass: caregiver-app entry sheet/timeline (48pt touch targets, status
  icon+color never color alone, per `design-system.md`), director-web Milestones tab
  (keyboard-navigable, visible focus rings, per `platform-rules.md`'s Director Web section), and
  parent-mobile Development section (48pt touch targets)
- [X] T055 Run quickstart.md's scenarios manually/via integration tests and confirm each passes,
  including the age-band boundary case and the parent cross-family rejection case
- [X] T056 Confirm FR-003/research.md R3 explicitly: grep the full diff for this feature for any
  update/delete route or MediatR command touching `child_milestone_observations` — none should
  exist

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: T005-T012 (entities/migrations) build sequentially per the listed
  `depends on` notes; T013-T017 (portfolio builder, PDF port, mapper, tests) depend on the
  entities existing. BLOCKS all user stories.
- **User Stories (Phase 3-6)**: All depend on Foundational (Phase 2) completion.
  - US1 (P1) has no dependency on US2/US3/US4 — recording is independently testable via the API/
    caregiver app.
  - US2 (P1) depends on observations existing (from US1, or seeded directly per its own
    Independent Test) but not on US1's UI.
  - US3 (P2) depends on the same shared `MilestonePortfolioBuilder` (Foundational) as US2, not on
    US2's web UI — its own tests seed observations directly.
  - US4 (P3) depends on `MilestonePortfolioBuilder` (Foundational) and the director/parent query
    handlers (US2/US3) existing, since the PDF reuses their authorization/data-shape, but is
    otherwise independently testable against seeded data.
- **Polish (Phase 7)**: Depends on all four user stories being complete.

### Parallel Opportunities

- T001-T004 (Setup) can all run in parallel.
- T005, T006, T013, T014 (Foundational, independent files) can run in parallel.
- T016, T017 (Foundational tests) can run in parallel.
- T018-T021 (US1 tests) can run in parallel.
- T031, T032 (US2 tests) can run in parallel.
- T038, T039 (US3 tests) can run in parallel.
- T046, T047 (US4 tests) can run in parallel.

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: run quickstart.md's caregiver-recording scenario against seeded test
   data
5. Demo if ready — recording works technically, even before director/parent views exist

### Incremental Delivery

1. Setup + Foundational → catalog + seed, observation entity, shared portfolio builder ready
2. Add User Story 1 → validate independently → demo (caregivers can record)
3. Add User Story 2 → validate independently → demo (directors can review — combined with US1,
   the record-then-review loop is real)
4. Add User Story 3 → validate independently → demo (parents can view their own child's
   portfolio)
5. Add User Story 4 → validate independently → demo (PDF export available to directors/parents)
6. Polish (Phase 7) → run all quickstart.md scenarios end to end, including the age-band boundary
   and cross-family rejection checks

---

## Phase 8: Convergence

- [X] T057 Wire an optimistic pending-sync entry for a milestone observation recorded while
  offline into `MilestoneTimeline` (mirrors `EventTimeline`'s `syncStatusByEventId` /
  `handleIncidentSaved`'s pattern in `mobile/app/(app)/child/[id].tsx`) and add the missing
  offline/pending-sync + top-of-history test coverage from T030, per spec.md's "Offline
  behavior" UX requirement and US1/AC3 (partial)

---

## Notes

- [P] tasks touch different files, or the same file in a way that doesn't conflict with other
  in-flight [P] tasks in the same phase.
- [Story] label maps each task to its user story for traceability.
- T021 exists specifically because immutability here is structural, not policy-enforced
  (research.md R3) — the test proves the absence of a mutation path, not just that one is
  rejected.
- Commit after each task or logical group, per this repo's standing convention.
