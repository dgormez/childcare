# Implementation Plan: Staff HR Dossier & Time Registration

**Branch**: `028-staff-hr-dossier` | **Date**: 2026-07-23 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/028-staff-hr-dossier/spec.md`

## Summary

Add two new tenant-schema tables — `staff_time_entries` (clock in/out, per-entry function,
computed 7-day lock with an explicit director unlock override) and `staff_documents` (HR
dossier: contracts, amendments, qualification/training records, GCS signed-URL storage) — plus a
new `StaffProfile.TimeEntryFunctions` field. Extend `staff-mobile` (feature 027) with a clock
in/out action on its home (schedule) screen. Build director-web's first staff detail screen
(`staff/[id]`, Dossier + Tijdsregistraties tabs), a new dashboard contract-expiry block mirroring
`DueSoonBlock`, and a new medewerkersbeleid subsidy report added to feature 018's existing
`/api/reports` group — computed from `AttendanceRecord` (child-hours) and `StaffTimeEntry`
(staff-hours), ratios only, no pass/fail evaluation (Clarifications — that's feature 041's job).

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Next.js 15 App Router (web admin
extensions), TypeScript / React Native (Expo, `staff-mobile` extension)

**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core 9, MediatR, FluentValidation
(backend); Tailwind, shadcn/ui, openapi-fetch, next-intl (web); Expo Router, NativeWind,
`lucide-react-native`, i18next/`expo-localization`, openapi-fetch (staff-mobile — no new
dependencies, all already present from feature 027)

**Storage**: PostgreSQL 16, tenant schema — new tables `staff_time_entries`, `staff_documents`;
extends `staff_profiles` with `TimeEntryFunctions`

**Testing**: xUnit + Moq + TestContainers-provisioned PostgreSQL (backend, Principle V); Vitest +
`@testing-library/react` (web, jsdom); Jest + `@testing-library/react-native` (staff-mobile)

**Target Platform**: Cloud Run (backend API), browser (director web, desktop ≥1280px), iOS/
Android via Expo (staff-mobile, phone/portrait)

**Project Type**: Web application + existing mobile app extension — backend API + Next.js
director-web additions + `staff-mobile` extension (no new client project)

**Performance Goals**: Subsidy report aggregates time entries and attendance records over a
date-bounded (typically 1-month) period for one location — small scale (tens of staff, hundreds
of attendance rows), no new performance-sensitive path; supported by the `(LocationId,
ClockedInAt)` and `(DocumentType, ValidUntil)` indexes (data-model.md).

**Constraints**: Clock in/out MUST resolve identity from the JWT, never a client-supplied staff
ID (research.md R2, mirrors feature 027's FR-015 precedent); an entry MUST reject mutation once
locked except through the explicit unlock endpoint (FR-006/FR-007); the subsidy report MUST
exclude open (unclosed) time entries and incomplete attendance records from hour totals rather
than estimate them (FR-019, research.md R5).

**Scale/Scope**: Same single-tenant scale as every prior feature (tens of staff, 1–3 locations
per organisation in Phase 1). Two new backend endpoint groups
(`StaffTimeEntryEndpoints`/dossier additions to `StaffEndpoints`), two new director-web screens
(`staff/[id]` detail, `reports/staff-hours`), one new dashboard block, one new staff-mobile UI
addition (no new staff-mobile screen — added to the existing schedule screen, research.md R10).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation | PASS | `staff_time_entries` and `staff_documents` both live in the tenant schema, no explicit tenant FK column — same structural pattern as every existing tenant table. All access goes through `TenantDbContext`/`ITenantDbContext`. No new cross-tenant surface. |
| II. Regulatory Compliance by Design (BKR) | PASS | This feature does not implement or modify BKR ratio *enforcement* — it reports computed child-hours/staff-hours ratios for a different regulatory purpose (the medewerkersbeleid subsidy, not the erkenning-linked BKR cap `GetBkrRatioQuery` enforces). Per Clarifications, the report deliberately does not evaluate pass/fail against Opgroeien's thresholds, precisely to avoid forking that logic ahead of feature 041's versioned ruleset — reviewed against Principle II's "regulation is time-versioned" clause and found consistent with it, not a violation of it. |
| III. CQRS via MediatR & Thin Endpoints | PASS | All writes (clock in/out, document upload/delete, time-entry correction/unlock/relock, function configuration) are MediatR commands; all reads (time-entry list, document list, contracts-expiring, staff-hours report) are MediatR queries; endpoint files map HTTP only. |
| IV. Internationalization First | PASS | All new director-web and staff-mobile strings are locale keys (NL/FR/EN) — spec.md's Technical Requirements. API error responses return `errorKey` values (see contracts/staff-hr-api.md), matching every existing endpoint's pattern. |
| V. Test with Real Infrastructure | PASS | Backend integration tests run against TestContainers PostgreSQL — clock in/out (incl. the single-open-entry invariant), lock/unlock/relock, contract-expiry boundary (60 days, inclusive of past dates), subsidy-report aggregation (incl. open-entry/incomplete-attendance exclusion), CSV export parity with the on-screen report. |
| VI. Secure Configuration & Storage | PASS | `IStaffDocumentStorage` uses signed, time-limited GCS URLs only (research.md R3) — no public blob URLs, no bytes proxied through the API. New tenant migration follows the standard reviewed-SQL-script rollout, not auto-apply. Time-entry unlock/re-lock and document upload/delete are attributable to the acting director (`UnlockedBy`, `StaffDocument.CreatedBy`/`DeletedBy` — FR-007a/FR-012a, added during `/speckit-checklist`'s safety-focused pass), since these are the paths that bypass an otherwise-immutable or otherwise-permanent record. |
| VII. Monolith-First Simplicity | PASS | No new backend projects, no new client projects — all backend code lives in the existing five-project solution; `staff-mobile` is extended in place (feature 027's existing Expo project), not forked. |

No violations — Complexity Tracking table not needed.

## Project Structure

### Documentation (this feature)

```text
specs/028-staff-hr-dossier/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── staff-hr-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/
│   │   ├── StaffTimeEntry.cs                        # new
│   │   ├── StaffDocument.cs                          # new
│   │   └── StaffProfile.cs                           # extended: +TimeEntryFunctions
│   └── Enums/
│       ├── StaffTimeEntryFunction.cs                 # new: Kinderbegeleider/Logistiek/Verantwoordelijke
│       └── StaffDocumentType.cs                      # new: EmploymentContract/Amendment/Qualification/Training/Other
├── ChildCare.Application/
│   ├── Common/
│   │   ├── ITenantDbContext.cs                       # +DbSet<StaffTimeEntry>, +DbSet<StaffDocument>
│   │   └── IStaffDocumentStorage.cs                  # new port (research.md R3)
│   ├── StaffTimeEntries/                             # new folder
│   │   ├── StaffTimeEntryResult.cs
│   │   ├── ClockInCommand.cs                          # FR-001/FR-003/FR-004/FR-005
│   │   ├── ClockOutCommand.cs                         # FR-002
│   │   ├── UpdateStaffTimeEntryCommand.cs             # FR-006/FR-008/FR-009 (correction + overlap warning)
│   │   ├── UnlockStaffTimeEntryCommand.cs             # FR-007
│   │   ├── RelockStaffTimeEntryCommand.cs
│   │   ├── ListStaffTimeEntriesQuery.cs
│   │   └── UpdateStaffTimeEntryFunctionsCommand.cs    # FR-010, on StaffProfile
│   ├── StaffDocuments/                                # new folder
│   │   ├── StaffDocumentResult.cs
│   │   ├── CreateStaffDocumentUploadUrlCommand.cs     # FR-011
│   │   ├── CreateStaffDocumentCommand.cs               # FR-011
│   │   ├── DeleteStaffDocumentCommand.cs
│   │   ├── ListStaffDocumentsQuery.cs                  # FR-012
│   │   └── GetContractsExpiringQuery.cs                # FR-014
│   └── Reporting/                                      # existing folder (018), extended
│       ├── GetStaffHoursReportQuery.cs                 # new — FR-016/FR-017/FR-018/FR-019
│       └── ExportStaffHoursReportQuery.cs              # new — FR-020, reuses GetStaffHoursReportQuery (research.md R6)
├── ChildCare.Contracts/
│   ├── Requests/
│   │   ├── StaffTimeEntryRequests.cs                   # new
│   │   └── StaffDocumentRequests.cs                    # new
│   └── Responses/
│       ├── StaffTimeEntryResponses.cs                  # new
│       ├── StaffDocumentResponses.cs                   # new
│       └── StaffHoursReportResponses.cs                # new
├── ChildCare.Infrastructure/
│   ├── Storage/
│   │   └── GcsStaffDocumentStorage.cs                  # new — implements IStaffDocumentStorage
│   ├── Persistence/
│   │   ├── TenantDbContext.cs                          # +StaffTimeEntry/StaffDocument config, TimeEntryFunctions text[] conversion (mirrors ContractedDays)
│   │   └── Migrations/Tenant/                          # new EF migration: staff_time_entries, staff_documents
│   └── Reporting/
│       └── StaffHoursCsvWriter.cs                       # new — implements a new IStaffHoursCsvWriter (mirrors IAttendanceSummaryCsvWriter)
├── ChildCare.Api/
│   └── Endpoints/
│       ├── StaffTimeEntryEndpoints.cs                   # new
│       ├── StaffEndpoints.cs                            # extended: document routes, time-entry-functions, contracts-expiring
│       └── ReportingEndpoints.cs                        # extended: /api/reports/staff-hours(/export)
└── ChildCare.Api.Tests/
    ├── StaffTimeEntries/
    │   ├── ClockInOutTests.cs                            # new — incl. single-open-entry invariant, function selection
    │   ├── TimeEntryLockTests.cs                          # new — 7-day lock, unlock, relock
    │   └── TimeEntryOverlapWarningTests.cs                 # new — FR-009
    ├── StaffDocuments/
    │   ├── StaffDossierTests.cs                            # new — upload/list/delete
    │   └── ContractsExpiringTests.cs                       # new — 60-day boundary, past-due inclusion
    ├── Reporting/
    │   └── StaffHoursReportTests.cs                        # new — aggregation, open-entry exclusion, CSV parity, zero-division
    ├── TenantMigrationRolloutTests.cs                       # extended (research.md R7)
    └── VaccineRecords/LegacyVaccinationMigrationTests.cs     # extended (research.md R7)

web/
├── app/(app)/
│   ├── staff/
│   │   ├── page.tsx                                     # extended: row click navigates to detail (was inert)
│   │   └── [id]/page.tsx                                 # new — Dossier + Tijdsregistraties tabs
│   ├── dashboard/page.tsx                                # extended: +ContractExpiryBlock
│   └── staff-hours-report/page.tsx                        # new — flat top-level route (this
│                                                            #   codebase has no "Rapporten" parent
│                                                            #   nav; every report-like screen is
│                                                            #   its own flat Sidebar entry, and
│                                                            #   018's reports live inline on
│                                                            #   /dashboard, not a nested route) —
│                                                            #   location/period selector, ratio
│                                                            #   table, CSV download
├── components/
│   ├── staff/
│   │   ├── StaffDossierTab.tsx                            # new
│   │   ├── StaffDocumentForm.tsx                          # new
│   │   ├── StaffTimeEntriesTab.tsx                        # new
│   │   ├── TimeEntryCorrectionDialog.tsx                   # new — incl. overlap warning
│   │   ├── TimeEntryFunctionsForm.tsx                       # new
│   │   └── ContractExpiryBlock.tsx                          # new, mirrors DueSoonBlock.tsx (research.md R8)
│   └── reporting/
│       └── StaffHoursReportTable.tsx                        # new
├── components/Sidebar.tsx                                    # +staffHoursReport nav entry (flat, mirrors every other top-level item)
├── lib/generated/api-types.ts                                # regenerated (openapi-typescript)
└── i18n/locales/{en,fr,nl}.json                               # +staff.dossier.*/staff.timeEntries.*/staffHoursReport.* keys

staff-mobile/
├── app/(app)/schedule/index.tsx                           # extended: +ClockInOutCard at top (research.md R10)
├── components/
│   └── ClockInOutCard.tsx                                  # new
├── services/
│   └── timeEntries.ts                                       # new — clock-in/clock-out API calls, mirrors schedule.ts's shape
├── services/generated/                                       # regenerated (openapi-fetch client)
├── i18n/locales/{en,fr,nl}.json                               # +timeEntries.* keys
└── __tests__/
    └── ClockInOutCard.test.tsx                               # new
```

**Structure Decision**: Backend changes add two new `Application` folders
(`StaffTimeEntries/`, `StaffDocuments/`) alongside the existing `StaffScheduling/`
convention, and extend feature 018's existing `Reporting/`/`ReportingEndpoints.cs` module for the
subsidy report rather than inventing a new reporting surface — per Constitution VII, no new
backend project. `staff-mobile` is extended in place (no new client project); director-web adds its first staff
detail screen and a new flat top-level route (`staff-hours-report`), matching every existing
Sidebar entry's convention rather than introducing a nested `reports/` section that has no
precedent in this codebase.

## Complexity Tracking

*No Constitution Check violations — table not needed.*
