# Tasks: Incident Reports

**Input**: Design documents from `specs/013b-incident-reports/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Required by constitution Principle V (real PostgreSQL via TestContainers for backend
integration tests; component tests for mobile/web), same standard every prior feature has followed.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Contracts DTOs and i18n scaffolding shared across all stories.

- [X] T001 [P] Add `FileIncidentReportRequest`/`UpdateIncidentReportRequest` to `backend/ChildCare.Contracts/Requests/IncidentReportRequests.cs`
- [X] T002 [P] Add `IncidentReportResponse` to `backend/ChildCare.Contracts/Responses/IncidentReportResponse.cs`
- [X] T003 [P] Add `incidentReports.*` i18n keys (form labels, injury-type chip labels, location-detail chip labels, validation messages, offline/pending-sync copy) to `mobile/i18n/locales/en.json`, `mobile/i18n/locales/fr.json`, `mobile/i18n/locales/nl.json`
- [X] T004 [P] Add `incidents.*` i18n keys (nav label, table column headers, filter labels, detail view field labels, PDF export button, reviewed/unreviewed badge, locked-field messaging) to `web/i18n/locales/en.json`, `web/i18n/locales/fr.json`, `web/i18n/locales/nl.json`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The `IncidentReport` entity, its enums, and the migration every story depends on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 [P] Create `IncidentInjuryType` enum (`None`, `Scrape`, `Bump`, `Cut`, `Fall`, `Bite`, `Burn`, `AllergicReaction`, `Other`) in `backend/ChildCare.Domain/Enums/IncidentInjuryType.cs`
- [X] T006 [P] Create `ParentNotifiedHow` enum (`Phone`, `App`, `InPerson`) in `backend/ChildCare.Domain/Enums/ParentNotifiedHow.cs`
- [X] T007 Create `IncidentReport` entity per data-model.md (all fields incl. `ReportedBy` as `List<Guid>`, `ReviewedAt`, `LocationId`) in `backend/ChildCare.Domain/Entities/IncidentReport.cs`
- [X] T008 Configure the `IncidentReport` entity (enum-as-text conversions mirroring `ChildEvent`'s existing convention, `ReportedBy` as a Postgres `uuid[]`, indexes on `(LocationId, OccurredAt)` and `(ChildId, OccurredAt)` per data-model.md) in `backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`
- [X] T009 Add tenant migration `AddIncidentReports` in `backend/ChildCare.Infrastructure/Persistence/Migrations/Tenant/`
- [X] T010 [P] Create `IncidentReportMapper` (entity ↔ response, multi-word enum wire-string mapping mirroring `ChildEventTypeExtensions`'s convention for `AllergicReaction`/`InPerson`) in `backend/ChildCare.Application/IncidentReports/IncidentReportMapper.cs`
- [X] T011 [P] Create `IncidentReportResult`/`IncidentReportFailure` (NotFound, ValidationFailed, Locked) in `backend/ChildCare.Application/IncidentReports/IncidentReportResult.cs`

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Caregiver files an incident report on the spot (Priority: P1) 🎯 MVP

**Goal**: A caregiver can file a complete incident report from a child's tablet profile, with
`reportedBy` resolved server-side (no PIN step, no offline block) and required fields enforced.

**Independent Test**: Submit `POST /api/incident-reports` with description + injury type and
verify a `201` with `reportedBy` populated from the checked-in shift register; submit with a
missing required field and verify a `400` naming it.

### Tests for User Story 1

- [X] T012 [P] [US1] Integration test: `POST /api/incident-reports` with description+injuryType creates a report with `ReportedBy` resolved via `IShiftAttributionService` from the checked-in caregiver (FR-001, FR-004) in `backend/ChildCare.Api.Tests/IncidentReports/FileIncidentReportTests.cs`
- [X] T013 [P] [US1] Integration test: `POST` missing `description` or `injuryType` returns `400` with field-specific error keys (FR-002) in `backend/ChildCare.Api.Tests/IncidentReports/FileIncidentReportTests.cs`
- [X] T014 [P] [US1] Integration test: `POST` with a `childId` that doesn't resolve within the tenant returns `404` in `backend/ChildCare.Api.Tests/IncidentReports/FileIncidentReportTests.cs`
- [X] T015 [P] [US1] Integration test: `POST` with no caregiver checked in on the device results in `reportedBy = []` and a `201` (never blocked) (FR-004) in `backend/ChildCare.Api.Tests/IncidentReports/FileIncidentReportTests.cs`
- [X] T016 [P] [US1] Integration test: a client-supplied `reportedBy` value in the request body is ignored/overwritten server-side (FR-004) in `backend/ChildCare.Api.Tests/IncidentReports/FileIncidentReportTests.cs`
- [X] T017 [P] [US1] Integration test: `POST` with a backdated `occurredAt` earlier than `createdAt` is accepted and both timestamps remain independently retrievable (FR-003) in `backend/ChildCare.Api.Tests/IncidentReports/FileIncidentReportTests.cs`

### Implementation for User Story 1

- [X] T018 [US1] Implement `FileIncidentReportCommand` + `FileIncidentReportCommandValidator` (description/injuryType required, per FluentValidation pipeline) in `backend/ChildCare.Application/IncidentReports/FileIncidentReportCommand.cs`
- [X] T019 [US1] Implement `FileIncidentReportCommandHandler`: resolve the device's `LocationId`/`GroupId`, call `IShiftAttributionService.ResolveRecordedByAsync` for `ReportedBy` (research.md R1), persist, return success in the same file
- [X] T020 [US1] Implement `GetIncidentReportQuery` + handler (read-only for now; the reviewed-on-open side effect is added in US2) — authorized for `DirectorOnly` or a device token whose paired location/group currently has the child assigned (FR-018, not restricted to reports that device itself filed) in `backend/ChildCare.Application/IncidentReports/GetIncidentReportQuery.cs`
- [X] T021 [US1] Wire `POST /api/incident-reports` and `GET /api/incident-reports/{id}` in `backend/ChildCare.Api/Endpoints/IncidentReportEndpoints.cs`, and register the endpoint group per this project's existing convention
- [X] T022 [US1] Regenerate and commit `mobile/services/generated/api-types.ts` against the new endpoints
- [X] T023 [P] [US1] Create `IncidentReportForm.tsx` (injury-type chips, location-detail chips, description field, first-aid/doctor/parent-notification fields, required-field validation, 48pt+ touch targets per design-system.md) in `mobile/components/IncidentReportForm.tsx`
- [X] T024 [US1] Create `mobile/services/incidentReports.ts`: `fileIncidentReport()` API call + `registerSyncHandler("incident_report", { onConflict: () => "discard" })` (mirrors `childEvents.ts`'s registration exactly; offline behavior itself is verified in US4). A same-device online submission failing for a non-network reason (e.g. `5xx`) surfaces as a normal retryable error and is NOT written to the offline queue — only an actual network-unreachable condition (per `useNetworkStatus()`) queues (FR-014).
- [X] T025 [US1] Add an "Incident melden" entry point on the child profile that opens `IncidentReportForm` in `mobile/app/(app)/child/[id].tsx`
- [X] T026 [P] [US1] Mobile component test: submitting with description+injuryType calls the API and closes the form; submitting with a missing required field shows a validation error in `mobile/__tests__/components/IncidentReportForm.test.tsx`

**Checkpoint**: A caregiver can file a report end-to-end (persisted, retrievable via the API),
independent of any director-facing UI.

---

## Phase 4: User Story 2 - Director reviews and exports incident reports (Priority: P1)

**Goal**: A director can list, filter, review, and PDF-export incident reports across every
location in their organisation.

**Independent Test**: File two reports for different children/locations; verify a director sees
both in the list, can filter down to one, and can export a PDF containing every field.

### Tests for User Story 2

- [X] T027 [P] [US2] Integration test: `GET /api/incident-reports` (`DirectorOnly`) returns every report across locations, sorted `occurredAt` descending by default (FR-009) in `backend/ChildCare.Api.Tests/IncidentReports/ListIncidentReportsFilterTests.cs`
- [X] T028 [P] [US2] Integration test: `GET /api/incident-reports?childId=&locationId=&from=&to=` filters correctly, individually and combined (FR-009) in `backend/ChildCare.Api.Tests/IncidentReports/ListIncidentReportsFilterTests.cs`
- [X] T029 [P] [US2] Integration test: a non-director caller (device token) is rejected on `GET /api/incident-reports` (cross-KDV list is `DirectorOnly`) in `backend/ChildCare.Api.Tests/IncidentReports/ListIncidentReportsFilterTests.cs`
- [X] T030 [P] [US2] Integration test: `GET /api/incident-reports/{id}` as a director sets `reviewedAt` on the first call and leaves it unchanged on subsequent calls (FR-010, FR-011) in `backend/ChildCare.Api.Tests/IncidentReports/ReviewedStateTests.cs`
- [X] T031 [P] [US2] Integration test: `GET /api/incident-reports/{id}/pdf` returns `application/pdf` bytes containing the location's name/address/`Dossiernummer` and every report field (FR-012) in `backend/ChildCare.Api.Tests/IncidentReports/GenerateIncidentReportPdfTests.cs`

### Implementation for User Story 2

- [X] T032 [US2] Implement `ListIncidentReportsQuery` + handler (default page size 25, secondary sort by `id` for stable pagination on equal `occurredAt`, `childId`/`locationId`/date-range filters, `DirectorOnly`, per FR-009) in `backend/ChildCare.Application/IncidentReports/ListIncidentReportsQuery.cs`
- [X] T033 [US2] Extend `GetIncidentReportQuery`'s handler to set `ReviewedAt = now` when the caller is a director and it is currently null (research.md R3) in `backend/ChildCare.Application/IncidentReports/GetIncidentReportQuery.cs`
- [X] T034 [US2] Implement `IIncidentReportPdfGenerator` in `backend/ChildCare.Application/Common/IIncidentReportPdfGenerator.cs` and `QuestPdfIncidentReportGenerator` (mirrors `QuestPdfContractGenerator`'s structure exactly, research.md R2) in `backend/ChildCare.Infrastructure/Pdf/QuestPdfIncidentReportGenerator.cs`
- [X] T035 [US2] Implement `GenerateIncidentReportPdfQuery` (loads report + location, builds the PDF model, calls the generator) in `backend/ChildCare.Application/IncidentReports/GenerateIncidentReportPdfQuery.cs`
- [X] T036 [US2] Wire `GET /api/incident-reports` (list) and `GET /api/incident-reports/{id}/pdf` in `backend/ChildCare.Api/Endpoints/IncidentReportEndpoints.cs`
- [X] T037 [US2] Regenerate and commit `web/lib/generated/api-types.ts` against the new endpoints
- [X] T038 [P] [US2] Create `IncidentReportsTable.tsx` (child, location, occurred-at, injury type, reviewed/unreviewed indicator columns — indicator MUST pair an icon with color, never color alone, per FR-010/design-system.md; mirrors `StaffTable.tsx`/`DayReservationsTable.tsx` pattern) in `web/components/IncidentReportsTable.tsx`
- [X] T039 [P] [US2] Create `IncidentReportFilters.tsx` (date range, location, child selectors — the location selector MUST include deactivated locations, since their historical incident reports remain reachable per spec Edge Cases) in `web/components/IncidentReportFilters.tsx`
- [X] T040 [US2] Create the Incidents list page (loads `GET /api/incident-reports` with filters, renders `IncidentReportsTable` + `IncidentReportFilters`, loading/empty/error states per platform-rules.md director-web density) in `web/app/(app)/incidents/page.tsx`
- [X] T041 [US2] Create the incident detail page (all fields, PDF export button; the `GET` call itself triggers the reviewed-on-open side effect) in `web/app/(app)/incidents/[id]/page.tsx`
- [X] T042 [US2] Add `incidents` to `REAL_NAV` in `web/components/Sidebar.tsx`
- [X] T043 [P] [US2] Web component test: list renders rows with the unreviewed indicator, filters narrow results, opening detail clears the indicator in `web/__tests__/incidentReports.test.tsx`

**Checkpoint**: A director can fully review, filter, and export incident reports end to end.

---

## Phase 5: User Story 3 - Director adds a follow-up note without altering the original record (Priority: P2)

**Goal**: Reports lock after 24 hours (everything except `follow_up`), enforced server-side
regardless of client input.

**Independent Test**: On a report older than 24 hours, attempt to edit its description via the
API and verify rejection; add a follow-up note and verify it saves with the original untouched.

### Tests for User Story 3

- [X] T044 [P] [US3] Integration test: `PUT /api/incident-reports/{id}` on a report older than 24h changing `description`/`injuryType`/etc. returns `409 errors.incident_reports.locked` and leaves the record unchanged (FR-005) in `backend/ChildCare.Api.Tests/IncidentReports/IncidentReportImmutabilityTests.cs`
- [X] T045 [P] [US3] Integration test: `PUT` with only `followUp` set on a report older than 24h succeeds regardless of age (FR-006) in `backend/ChildCare.Api.Tests/IncidentReports/IncidentReportImmutabilityTests.cs`
- [X] T046 [P] [US3] Integration test: `PUT` on a report younger than 24h accepts any field change from the reporting caregiver or a director (FR-007) in `backend/ChildCare.Api.Tests/IncidentReports/IncidentReportImmutabilityTests.cs`
- [X] T047 [P] [US3] Integration test: editing a reviewed report (within or after the 24h window) does not reset `reviewedAt` (Clarifications) in `backend/ChildCare.Api.Tests/IncidentReports/IncidentReportImmutabilityTests.cs`
- [X] T048 [P] [US3] Integration test: an `IncidentReport`'s rows remain fully retrievable (list + detail) after its child's `DeactivatedAt` is set (FR-008) in `backend/ChildCare.Api.Tests/IncidentReports/IncidentReportChildDeactivationTests.cs`

### Implementation for User Story 3

- [X] T049 [US3] Implement `UpdateIncidentReportCommand` + `UpdateIncidentReportCommandValidator` in `backend/ChildCare.Application/IncidentReports/UpdateIncidentReportCommand.cs`
- [X] T050 [US3] Implement the handler: return `Locked` if `CreatedAt` is more than 24h old and any field other than `FollowUp` is present in the request; otherwise apply all included fields; never touch `ReviewedAt`, in the same file
- [X] T051 [US3] Wire `PUT /api/incident-reports/{id}` (mapping `Locked` → `409 errors.incident_reports.locked`) in `backend/ChildCare.Api/Endpoints/IncidentReportEndpoints.cs`
- [X] T052 [P] [US3] Add a follow-up note field + save action to the incident detail page (works regardless of report age) in `web/app/(app)/incidents/[id]/page.tsx`
- [X] T053 [P] [US3] Web component test: a follow-up note saves on a locked (>24h) report while every other field renders read-only in `web/__tests__/incidentReports.test.tsx`

**Checkpoint**: The feature's core legal-integrity requirement (immutability) is enforced in
practice, not just in theory.

---

## Phase 6: User Story 4 - Incident filed offline is captured and synced without loss (Priority: P2)

**Goal**: Filing an incident report never depends on network availability; queued reports sync
automatically on reconnect with no data loss.

**Independent Test**: Disable network, file a report, verify it appears locally with a
pending-sync indicator; re-enable network and verify it syncs and becomes visible to a director.

### Tests for User Story 4

- [X] T054 [P] [US4] Mobile test: submitting the incident form while offline queues to `offline_queue` (`entity_type = "incident_report"`), shows immediately in the child's local view with a pending-sync indicator, and surfaces no error in `mobile/__tests__/services/incidentReports.test.ts`
- [X] T055 [P] [US4] Mobile test: on reconnect, the sync engine replays the queued `incident_report` entry via the registered handler and the pending-sync indicator clears in `mobile/__tests__/services/incidentReports.test.ts`
- [X] T056 [P] [US4] Mobile test: a backdated `occurredAt` (offline-authored, filed after the fact once reconnected) syncs successfully with `occurredAt`/`createdAt` remaining distinct once confirmed (spec Edge Cases) in `mobile/__tests__/services/incidentReports.test.ts`

### Implementation for User Story 4

- [X] T057 [US4] Confirm/extend `mobile/services/incidentReports.ts`'s offline-queue write path (optimistic local record) — reuse the exact pattern `childEvents.ts` already established for optimistic offline rows
- [X] T058 [US4] Merge queued incident reports with server-confirmed ones in the child profile's local view, rendering the pending-sync badge (mirrors `EventTimeline`'s existing merge logic) in `mobile/app/(app)/child/[id].tsx`
- [X] T059 [P] [US4] Mobile component test: the pending-sync badge appears on an unsynced incident report and clears once the sync engine confirms it in `mobile/__tests__/components/IncidentReportForm.test.tsx`

**Checkpoint**: All four user stories are independently functional. Full feature complete.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T060 [P] Audit every new mobile/web string introduced by this feature for i18n-key usage (no hardcoded text) across NL/FR/EN (constitution Principle IV)
- [X] T061 Run the full backend + mobile + web test suites and fix any regressions surfaced by the new table/endpoints/screens

---

## Phase 8: Convergence

- [X] T062 Add pagination controls (next/previous, page indicator) to `web/app/(app)/incidents/page.tsx`, using `PagedIncidentReportsResponse`'s `page`/`pageSize`/`totalCount` fields the API already returns, per FR-009 (partial)

---

## Dependencies & Execution Order

- **Phase 1 (Setup)** → **Phase 2 (Foundational)**: strictly sequential, blocks everything below.
- **Phase 3 (US1)**: depends only on Phase 2. Delivers end-to-end filing (MVP).
- **Phase 4 (US2)**: depends only on Phase 2's entity/migration, not on US1's mobile UI — its
  independent test is API-level, so it can be staffed in parallel with US1. `GetIncidentReportQuery`
  (T020, US1) is extended in place by T033 (US2), so if sequencing rather than parallelizing,
  complete US1 first.
- **Phase 5 (US3)**: depends on Phase 2 (entity) and reuses `GetIncidentReportQuery`/detail page
  from US1/US2 — implement after both for a clean sequential build, though its own tests are
  independent of US1/US2's UI.
- **Phase 6 (US4)**: depends on US1's `mobile/services/incidentReports.ts` (T024) and
  `IncidentReportForm.tsx` (T023) existing already.
- **Phase 7 (Polish)**: depends on all prior phases being complete.

## Parallel Execution Examples

- Within Phase 1: T001–T004 touch disjoint files — run together.
- Within Phase 2: T005/T006 (enums) in parallel; T010/T011 in parallel once T007 lands.
- Within Phase 3: T012–T017 (tests) in parallel; T023 (mobile form) in parallel with backend tasks
  T018–T021 once the API contract (already fixed in contracts/incident-reports-api.md) is known.
- Within Phase 4: T027–T031 (tests) in parallel; T038/T039 (independent new components) in
  parallel before T040/T041 (which consume them).
- US1 (Phase 3) and US2 (Phase 4) can be staffed in parallel by two independent workstreams once
  Phase 2 lands, since Phase 2 is their only shared dependency.

## Implementation Strategy

**MVP = Phase 1 + 2 + 3 (User Story 1)**: a caregiver can file a complete incident report
end-to-end, persisted and retrievable via the API. Per spec.md's own "Why this priority"
reasoning, US1 and US2 are equally load-bearing P1s (the record has no value until a director can
find it) — ship both before considering the feature done. US3 (immutability) and US4 (offline) are
P2 safety/reliability guarantees that genuinely can follow after, though both are required before
this feature can be trusted in real KDV conditions per their own spec reasoning.
