# Tasks: Management Reporting

**Input**: Design documents from `specs/018-management-reporting/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required by constitution Principle V (real PostgreSQL via TestContainers for backend
integration tests; component tests for web), same standard every prior feature has followed.

**Organization**: Tasks are grouped by user story to enable independent implementation and
testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Response contracts and i18n scaffolding shared across all five report sections.

- [X] T001 [P] Add response DTOs per contracts/management-reporting-api.md
  (`OccupancyLocationSummaryResponse`, `OccupancyGroupSummaryResponse`, `BkrGroupRatioResponse`,
  `BkrBreachResponse`, `AttendanceSummaryRowResponse`, `AttendanceSummaryResponse`,
  `InvoiceStatusOverviewResponse`, `OverdueInvoiceResponse`, `DataCompletenessFlagResponse`) in
  `backend/ChildCare.Contracts/Responses/ReportingResponses.cs`
- [X] T002 [P] Add director-web `dashboard.reporting.*` i18n keys (section titles, status labels
  green/amber/red with their paired icon names per design-system.md, empty states — "no overdue
  invoices," "no breaches in this period," "nothing to flag" — location filter label, CSV/PDF
  export actions) to `web/i18n/locales/en.json`, `web/i18n/locales/fr.json`,
  `web/i18n/locales/nl.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The one schema change every occupancy-related story needs, plus shared backend
scaffolding.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T003 Add `Capacity` (`int?`) to `Group` in `backend/ChildCare.Domain/Entities/Group.cs`
  per data-model.md
- [X] T004 Map `Group.Capacity` (nullable column) in
  `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs` (depends on T003)
- [X] T005 Add tenant migration `AddGroupCapacity` in
  `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/` (depends on T004)
- [X] T006 Extend `TenantMigrationRolloutTests`' schema-revert helper for the new column (the
  recurring pattern every migration-adding feature since 003 has needed) in
  `backend/ChildCare.Api.Tests/TenantMigrationRolloutTests.cs` (depends on T005)
- [X] T007 [P] Add indexes needed for efficient date-range aggregation if not already present —
  `AttendanceRecord (LocationId, Date)` and `Invoice (Status, DueDate)` — in a tenant migration
  `AddReportingIndexes` in `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/`
  (check existing indexes first; only add what's missing)
- [X] T008 [P] Add `ReportingMapper` (shared response-mapping helpers: BKR status
  green/amber/red, occupancy status, days-overdue calculation) in
  `backend/ChildCare.Application/Reporting/ReportingMapper.cs`
- [X] T009 [P] Add `ReportingEndpoints.cs` skeleton (route group, `DirectorOnly` policy per
  contracts/management-reporting-api.md, no handlers yet) in
  `backend/ChildCare.Api/Endpoints/ReportingEndpoints.cs`
- [X] T010 Register `app.MapReportingEndpoints()` in `backend/ChildCare.Api/Program.cs` (depends
  on T009)
- [X] T011 [P] Add `LocationFilter.tsx` shared component (single-select location dropdown,
  narrows every dashboard section, per FR-013) in `web/components/reporting/LocationFilter.tsx`

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel.

---

## Phase 3: User Story 1 - Director checks today's occupancy and BKR compliance at a glance (Priority: P1) 🎯 MVP

**Goal**: Colour-coded today/week-ahead occupancy per group and location, plus live per-group
BKR ratio, on the director dashboard.

**Independent Test**: Seed a tenant with two locations, groups both under and over capacity, and
staffing both within and breaching BKR; confirm the dashboard shows correct colour-coded status
per group/location and accurate live BKR ratios, filterable to one location.

### Tests for User Story 1 ⚠️

- [X] T012 [P] [US1] Integration test: `GET /api/reports/occupancy` returns correct
  green/amber/red per group against `Group.Capacity` and per location against
  `Location.MaxCapacity`, `0/capacity` (not an error) for a location with a published closure
  today, and a `weekAhead` array matching `GetOccupancyQuery`'s existing contract-based
  projection for the same location (FR-003), in
  `backend/ChildCare.Api.Tests/Reporting/OccupancyEndpointsTests.cs`
- [X] T013 [P] [US1] Integration test: a group with no `Capacity` set returns `capacity: null`,
  `status: null` (no divide-by-zero) in
  `backend/ChildCare.Api.Tests/Reporting/OccupancyEndpointsTests.cs`
- [X] T014 [P] [US1] Integration test: `GET /api/reports/bkr` returns the correct live ratio per
  group (present count, qualified staff count excluding `StudentVolunteer`, nap-time inference,
  threshold, status), matching `GetBkrRatioQuery`'s existing rules scoped down to one group, in
  `backend/ChildCare.Api.Tests/Reporting/BkrRatioEndpointsTests.cs`
- [X] T015 [P] [US1] Integration test: a director from tenant A cannot see tenant B's occupancy
  or BKR data via either endpoint, and a `locationId` belonging to another tenant is treated as
  no valid selection rather than leaking or substituting data (FR-013), in
  `backend/ChildCare.Api.Tests/Reporting/OccupancyEndpointsTests.cs`
- [X] T016 [P] [US1] Integration test: "today" resolves via `BelgianCalendarDay`, not a rolling
  24h window, for both endpoints (FR-016) in
  `backend/ChildCare.Api.Tests/Reporting/OccupancyEndpointsTests.cs`

### Implementation for User Story 1

- [X] T017 [US1] Implement `GetOccupancySummaryQuery` (today actual per group/location via
  `AttendanceRecord` + `ChildGroupAssignment`; week-ahead via `GetOccupancyQuery` reuse per
  research.md R1) in `backend/ChildCare.Application/Reporting/GetOccupancySummaryQuery.cs`
  (depends on T003, T008)
- [X] T018 [US1] Implement `GetGroupBkrRatioQuery` (per-group live ratio extending
  `GetBkrRatioQuery`'s pattern per research.md R2) in
  `backend/ChildCare.Application/Reporting/GetGroupBkrRatioQuery.cs` (depends on T008)
- [X] T019 [US1] Wire `GET /api/reports/occupancy` and `GET /api/reports/bkr` handlers (with
  `locationId` optional filter) in `backend/ChildCare.Api/Endpoints/ReportingEndpoints.cs`
  (depends on T017, T018, T009)
- [X] T020 [P] [US1] Implement `OccupancySection.tsx` (per-location + per-group colour-coded
  cards/rows with paired icon per status, week-ahead strip, per design-system.md's Status
  Indicators and spacing scale) in `web/components/reporting/OccupancySection.tsx`
- [X] T021 [P] [US1] Implement `BkrComplianceSection.tsx` (per-group live ratio, colour-coded
  with paired icon) in `web/components/reporting/BkrComplianceSection.tsx`
- [X] T022 [US1] Wire `OccupancySection`/`BkrComplianceSection`/`LocationFilter` into
  `web/app/(app)/dashboard/page.tsx` (depends on T011, T020, T021)
- [X] T023 [P] [US1] Component test: `OccupancySection` renders green/amber/red using the exact
  check-circle/clock/alert-triangle icon mapping FR-018 specifies (never colour alone) and a
  clean `0/capacity` empty state in `web/__tests__/dashboard.test.tsx` (extends the existing
  file)

**Checkpoint**: At this point, User Story 1 should be fully functional and testable
independently — the MVP.

---

## Phase 4: User Story 2 - Director reviews BKR breach history (Priority: P2)

**Goal**: For a director-chosen date range (default: last 30 days), show every BKR breach window
per group.

**Independent Test**: Seed historical attendance/staffing data with a known breach window;
confirm the breach history reports its start, end, and group correctly, and shows an empty state
for a range with no breaches.

### Tests for User Story 2 ⚠️

- [X] T024 [P] [US2] Integration test: `GET /api/reports/bkr/breaches` correctly reconstructs a
  known breach window's start/end from seeded `AttendanceRecord`/`RoomShift` timestamps, in
  `backend/ChildCare.Api.Tests/Reporting/BkrBreachHistoryEndpointsTests.cs`
- [X] T025 [P] [US2] Integration test: a range with no breaches returns an empty `breaches` array;
  the default range (no `from`/`to` supplied) is the last 30 days; a range exceeding 366 days
  returns 422 `errors.validation` (this codebase's standard FluentValidation status code) in
  `backend/ChildCare.Api.Tests/Reporting/BkrBreachHistoryEndpointsTests.cs`

### Implementation for User Story 2

- [X] T026 [US2] Implement `GetBkrBreachHistoryQuery` (on-demand reconstruction from
  `AttendanceRecord`/`RoomShift` check-in/out timestamps per research.md R3, default 30-day
  range, 366-day max per contracts/management-reporting-api.md) in
  `backend/ChildCare.Application/Reporting/GetBkrBreachHistoryQuery.cs` (depends on T008)
- [X] T027 [US2] Wire `GET /api/reports/bkr/breaches` handler in
  `backend/ChildCare.Api/Endpoints/ReportingEndpoints.cs` (depends on T026)
- [X] T028 [US2] Extend `BkrComplianceSection.tsx` with a breach-history sub-section (date-range
  picker, empty state "no breaches in this period") in
  `web/components/reporting/BkrComplianceSection.tsx` (depends on T021)
- [X] T029 [P] [US2] Component test: breach-history sub-section renders seeded breaches and the
  empty state correctly in `web/__tests__/dashboard.test.tsx`

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently.

---

## Phase 5: User Story 3 - Director generates and exports a monthly attendance summary (Priority: P1)

**Goal**: Monthly present/absent(justified/unjustified)/closure totals per child, rolled up per
group/location, exportable as CSV and PDF with totals matching the on-screen view exactly.

**Independent Test**: Seed a month of attendance spanning a mid-month contract/location change
for one child; confirm on-screen, CSV, and PDF all agree and correctly attribute every day across
the boundary.

### Tests for User Story 3 ⚠️

- [X] T030 [P] [US3] Integration test: `GET /api/reports/attendance-summary` aggregates
  present/absent-justified/absent-unjustified/closure days correctly per child, and rolls up
  correctly per group and per location, in
  `backend/ChildCare.Api.Tests/Reporting/AttendanceSummaryEndpointsTests.cs`
- [X] T031 [P] [US3] Integration test: a child whose location/group changes mid-month has each
  day attributed to the location/group actually active that day, with no day dropped or
  double-counted (data-model.md's Edge Case), in
  `backend/ChildCare.Api.Tests/Reporting/AttendanceSummaryEndpointsTests.cs`
- [X] T032 [P] [US3] Integration test: CSV export (`format=csv`) totals match the on-screen JSON
  response exactly, UTF-8 BOM encoded, in
  `backend/ChildCare.Api.Tests/Reporting/AttendanceSummaryExportTests.cs`
- [X] T033 [P] [US3] Integration test: PDF export (`format=pdf`) renders a valid PDF stream whose
  totals match the on-screen JSON response exactly in
  `backend/ChildCare.Api.Tests/Reporting/AttendanceSummaryExportTests.cs`
- [X] T033a [P] [US3] Integration test: a director from tenant A cannot retrieve tenant B's
  attendance summary or export via either endpoint (FR-012), and re-requesting an export after
  correcting an underlying `AttendanceRecord` reflects that correction immediately — never a
  cached/stale result (FR-022) — in
  `backend/ChildCare.Api.Tests/Reporting/AttendanceSummaryExportTests.cs`

### Implementation for User Story 3

- [X] T034 [US3] Implement `GetAttendanceSummaryQuery` (shared aggregation per data-model.md's
  `AttendanceSummaryRow`, feeds JSON/CSV/PDF per research.md R5) in
  `backend/ChildCare.Application/Reporting/GetAttendanceSummaryQuery.cs` (depends on T008)
- [X] T035 [P] [US3] Implement `CsvAttendanceSummaryWriter` (RFC 4180, UTF-8 BOM, comma-delimited
  per research.md R8) in `backend/ChildCare.Infrastructure/Reporting/CsvAttendanceSummaryWriter.cs`
- [X] T036 [P] [US3] Define `IAttendanceSummaryPdfGenerator` port + implement
  `QuestPdfAttendanceSummaryGenerator` (on-demand/unstored, mirrors `QuestPdfInvoiceGenerator`,
  per-locale labels per Constitution IV) in
  `backend/ChildCare.Application/Common/IAttendanceSummaryPdfGenerator.cs` and
  `backend/ChildCare.Infrastructure/Pdf/QuestPdfAttendanceSummaryGenerator.cs`
- [X] T037 [US3] Implement `ExportAttendanceSummaryQuery` (reuses `GetAttendanceSummaryQuery`'s
  result, dispatches to CSV writer or PDF generator by `format`) in
  `backend/ChildCare.Application/Reporting/ExportAttendanceSummaryQuery.cs` (depends on T034,
  T035, T036)
- [X] T038 [US3] Wire `GET /api/reports/attendance-summary` and
  `GET /api/reports/attendance-summary/export` handlers in
  `backend/ChildCare.Api/Endpoints/ReportingEndpoints.cs` (depends on T034, T037)
- [X] T039 [P] [US3] Implement `AttendanceSummarySection.tsx` (month picker, on-screen totals
  table per group/location, CSV/PDF export buttons per design-system.md's high-density director
  table conventions) in `web/components/reporting/AttendanceSummarySection.tsx`
- [X] T040 [US3] Wire `AttendanceSummarySection` into `web/app/(app)/dashboard/page.tsx` (depends
  on T022, T039)
- [X] T041 [P] [US3] Component test: `AttendanceSummarySection` renders totals and triggers
  CSV/PDF export requests correctly in `web/__tests__/dashboard.test.tsx`

**Checkpoint**: User Stories 1, 2, and 3 should now all work independently.

---

## Phase 6: User Story 4 - Director reviews invoice status overview (Priority: P2)

**Goal**: Current-month paid/outstanding/overdue invoice counts and totals, with an overdue list
showing days-overdue per invoice.

**Independent Test**: Seed invoices in Draft/Sent/Paid states for the current month, including
some Sent past due date; confirm the overview buckets and totals them correctly.

### Tests for User Story 4 ⚠️

- [X] T042 [P] [US4] Integration test: `GET /api/reports/invoices` correctly buckets
  paid/outstanding/overdue using the existing `Status`/`DueDate` convention (research.md R6) and
  computes correct revenue totals, in
  `backend/ChildCare.Api.Tests/Reporting/InvoiceStatusOverviewEndpointsTests.cs`
- [X] T043 [P] [US4] Integration test: overdue list shows correct `daysOverdue` per invoice; an
  empty overdue list returns an empty array in
  `backend/ChildCare.Api.Tests/Reporting/InvoiceStatusOverviewEndpointsTests.cs`
- [X] T043a [P] [US4] Integration test: a director from tenant A cannot retrieve tenant B's
  invoice status overview via this endpoint (FR-012) in
  `backend/ChildCare.Api.Tests/Reporting/InvoiceStatusOverviewEndpointsTests.cs`

### Implementation for User Story 4

- [X] T044 [US4] Implement `GetInvoiceStatusOverviewQuery` in
  `backend/ChildCare.Application/Reporting/GetInvoiceStatusOverviewQuery.cs` (depends on T008)
- [X] T045 [US4] Wire `GET /api/reports/invoices` handler in
  `backend/ChildCare.Api/Endpoints/ReportingEndpoints.cs` (depends on T044)
- [X] T046 [P] [US4] Implement `InvoiceStatusSection.tsx` (bucket counts/totals, overdue list
  linking to `web/app/(app)/invoices/[id]/page.tsx`, empty state) in
  `web/components/reporting/InvoiceStatusSection.tsx`
- [X] T047 [US4] Wire `InvoiceStatusSection` into `web/app/(app)/dashboard/page.tsx` (depends on
  T040, T046)
- [X] T048 [P] [US4] Component test: `InvoiceStatusSection` renders correct buckets, overdue list,
  and empty state in `web/__tests__/dashboard.test.tsx`

**Checkpoint**: User Stories 1–4 should now all work independently.

---

## Phase 7: User Story 5 - Director reviews the data-completeness monitor (Priority: P3)

**Goal**: A single flagged list — missing pickup contact, overdue vaccine, missing staff
qualification, missing staff PIN — each linking to the affected record.

**Independent Test**: Seed one case of each of the four gaps; confirm all four are flagged with a
clear reason and link; confirm a tenant with none of them shows the empty state.

### Tests for User Story 5 ⚠️

- [X] T049 [P] [US5] Integration test: `GET /api/reports/data-completeness` flags a child with no
  `CanPickup` contact, a child with an overdue `VaccineRecord` (`NextDueDate` passed, no newer
  record), a staff member missing `QualificationLevel`, and a staff member with no `PinHash`, per
  research.md R7, in `backend/ChildCare.Api.Tests/Reporting/DataCompletenessEndpointsTests.cs`
- [X] T050 [P] [US5] Integration test: a tenant with none of the four gaps returns an empty
  `flags` array in `backend/ChildCare.Api.Tests/Reporting/DataCompletenessEndpointsTests.cs`
- [X] T050a [P] [US5] Integration test: a director from tenant A cannot retrieve tenant B's
  data-completeness flags via this endpoint (FR-012) in
  `backend/ChildCare.Api.Tests/Reporting/DataCompletenessEndpointsTests.cs`

### Implementation for User Story 5

- [X] T051 [US5] Implement `GetDataCompletenessQuery` (four checks per research.md R7) in
  `backend/ChildCare.Application/Reporting/GetDataCompletenessQuery.cs` (depends on T008)
- [X] T052 [US5] Wire `GET /api/reports/data-completeness` handler in
  `backend/ChildCare.Api/Endpoints/ReportingEndpoints.cs` (depends on T051)
- [X] T053 [P] [US5] Implement `DataCompletenessSection.tsx` (flat flagged list, each linking to
  the child's or staff member's existing detail screen, empty state) in
  `web/components/reporting/DataCompletenessSection.tsx`
- [X] T054 [US5] Wire `DataCompletenessSection` into `web/app/(app)/dashboard/page.tsx` (depends
  on T047, T053)
- [X] T055 [P] [US5] Component test: `DataCompletenessSection` renders all four flag types with
  correct links, and the empty state, in `web/__tests__/dashboard.test.tsx`

**Checkpoint**: All five user stories should now be independently functional.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Cross-story correctness, error handling, and final validation.

- [X] T056 [P] Verify every new endpoint returns a locale-aware error key (never a raw stack
  trace) on failure, with the full error logged server-side (FR-019, Constitution VI) across
  `backend/ChildCare.Api/Endpoints/ReportingEndpoints.cs`
- [X] T057 [P] Verify each dashboard section loads independently (one section's failure/slow
  query never blocks another) with its own loading skeleton in
  `web/app/(app)/dashboard/page.tsx`
- [X] T058 [P] Verify every interactive element (filter, export button, drill-in row) is
  keyboard-reachable with a visible focus ring, per platform-rules.md's Director Web App section,
  across `web/components/reporting/`
- [X] T059 Run `quickstart.md` validation end-to-end against a local seeded tenant
- [X] T060 Update `Workflows/reporting.md` and `workflows.md` if implementation surfaced any
  discrepancy from what was documented during planning (governance rule — only if needed)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories (the
  `Group.Capacity` migration in particular is needed by US1's occupancy query).
- **User Stories (Phase 3–7)**: All depend on Foundational phase completion. US1 and US3 are both
  P1 (MVP scope together); US2 depends conceptually on US1's BKR section existing in the UI
  (T028 extends `BkrComplianceSection.tsx`) but its backend query (T026) has no code dependency
  on US1. US4 and US5 are fully independent of every other story.
- **Polish (Phase 8)**: Depends on all desired user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: No dependencies on other stories.
- **User Story 2 (P2)**: Backend independent; UI extends US1's `BkrComplianceSection.tsx` file
  (not a functional dependency — the file simply already exists once US1 ships).
- **User Story 3 (P1)**: No dependencies on other stories.
- **User Story 4 (P2)**: No dependencies on other stories.
- **User Story 5 (P3)**: No dependencies on other stories.

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel.
- All Foundational tasks marked [P] can run in parallel once T003–T006 (the migration chain)
  completes.
- Once Foundational completes, US1, US3, US4, US5 can all start in parallel; US2 can start in
  parallel too (its backend has no dependency on US1, only its UI task T028 waits on T021).
- All test tasks within a story marked [P] can run in parallel.

---

## Parallel Example: User Story 1

```bash
# Launch both integration test files together:
Task: "Integration test occupancy colour-coding in backend/ChildCare.Api.Tests/Reporting/OccupancyEndpointsTests.cs"
Task: "Integration test live BKR ratio in backend/ChildCare.Api.Tests/Reporting/BkrRatioEndpointsTests.cs"

# Launch both web sections together:
Task: "Implement OccupancySection.tsx in web/components/reporting/OccupancySection.tsx"
Task: "Implement BkrComplianceSection.tsx in web/components/reporting/BkrComplianceSection.tsx"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 3)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (critical — blocks all stories).
3. Complete Phase 3: User Story 1 (today's occupancy + live BKR).
4. Complete Phase 5: User Story 3 (monthly attendance summary + export) — both are P1; either
   order is fine since they're independent.
5. **STOP and VALIDATE**: Run quickstart.md's occupancy/BKR/attendance-summary sections.
6. Deploy/demo if ready.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 + US3 (both P1) → test independently → MVP.
3. US2 (BKR breach history, P2) → test independently.
4. US4 (invoice overview, P2) → test independently.
5. US5 (data-completeness monitor, P3) → test independently.
6. Polish phase → final cross-cutting validation.

---

## Notes

- [P] tasks = different files, no dependencies.
- [Story] label maps task to specific user story for traceability.
- This feature has no `mobile/` or `parent-mobile/` surface — director web only.
- Verify tests fail before implementing.
- Commit after each task or logical group.
- Stop at any checkpoint to validate a story independently.
