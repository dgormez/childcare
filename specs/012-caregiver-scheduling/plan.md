# Implementation Plan: Caregiver Scheduling (Weekly Staff Rota)

**Branch**: `012-caregiver-scheduling` | **Date**: 2026-07-10 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/012-caregiver-scheduling/spec.md`

## Summary

Build the `staff_schedules` planned-rota data model and a director-web rota builder
(create/edit/copy/mark-absent), plus a personal-account-scoped read endpoint for a
caregiver's own schedule (consumed later by feature 027, not this feature). During planning,
research confirmed feature 010's live BKR ratio (`GetBkrRatioQuery`) is sourced from
`RoomShifts` real-time check-in presence (feature 008a), not from any schedule — so this
feature does **not** wire `staff_schedules` into the live ratio. Instead it exposes a
separate, planning-only "projected on-duty count" for the rota builder's own use, keeping the
live BKR computation exactly as-is.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Next.js 15 App Router (web admin)

**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core 9, MediatR, FluentValidation
(backend); Tailwind, shadcn/ui, openapi-fetch, next-intl (web)

**Storage**: PostgreSQL 16, tenant schema — new `staff_schedules` table

**Testing**: xUnit + Moq + TestContainers-provisioned PostgreSQL (backend, constitution
Principle V — no InMemory provider); Vitest + `@testing-library/react` (web, jsdom
environment, per feature 007a's precedent)

**Target Platform**: Cloud Run (backend API), browser (director web, desktop ≥1280px)

**Project Type**: Web application — backend API + Next.js director-web admin (no mobile UI
in this feature; see spec.md Assumptions)

**Performance Goals**: Rota-builder week view (one location, ~20 staff × 7 days) loads and
renders without pagination; rota-copy is a single bulk-insert transaction, not N sequential
requests

**Constraints**: All writes `DirectorOnly`; overlap validation must be safe under concurrent
writes (reuse `IAdvisoryLockService`, feature 007's pattern, keyed on `staffId`); must not
read from or write to feature 010's `GetBkrRatioQuery`/`RoomShifts` path

**Scale/Scope**: Single-tenant scale (tens of staff, 1–3 locations per organisation in Phase
1) — no need for virtualized grids or server-side pagination at this scale

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation | PASS | `staff_schedules` lives in the tenant schema, no explicit tenant FK column — same structural pattern as `Location`/`StaffProfile` (004/005). All access goes through `TenantDbContext`/`ITenantDbContext`. |
| II. Regulatory Compliance by Design (BKR) | PASS | This feature does not implement or modify BKR enforcement — feature 010's `GetBkrRatioQuery` (RoomShifts-sourced) remains the sole live BKR computation, unchanged. This feature's "projected on-duty count" is explicitly non-authoritative planning data, not a regulatory enforcement path, so it does not engage this principle's NON-NEGOTIABLE gate. |
| III. CQRS via MediatR & Thin Endpoints | PASS | All writes (create/edit/delete schedule entry, copy week, mark absence) are MediatR commands; the projected on-duty count and own-schedule read are MediatR queries; `StaffScheduleEndpoints.cs` maps HTTP only. |
| IV. Internationalization First | PASS | All new director-web strings added as `next-intl` keys (NL/FR/EN); no hardcoded text. API error responses return `errorKey` values, matching every existing endpoint's pattern. |
| V. Test with Real Infrastructure | PASS | Backend integration tests run against TestContainers PostgreSQL, covering overlap-rejection (incl. concurrency), absence/projected-count behavior, closure-day exclusion on copy, past-date immutability, and own-schedule read scoping. |
| VI. Secure Configuration & Storage | N/A | No secrets, no file storage, no PDF in this feature. |
| VII. Monolith-First Simplicity | PASS | No new projects; new code lives in the existing five-project solution (`ChildCare.Domain`/`Application`/`Contracts`/`Api`/`Infrastructure`) and the existing `web/` app. |

No violations — Complexity Tracking table not needed.

## Project Structure

### Documentation (this feature)

```text
specs/012-caregiver-scheduling/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── staff-schedules-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/
│   │   └── StaffSchedule.cs                       # new
│   └── Enums/
│       └── AbsenceReason.cs                       # new
├── ChildCare.Application/
│   ├── Common/
│   │   └── ITenantDbContext.cs                    # +DbSet<StaffSchedule>
│   └── StaffScheduling/                            # new folder, mirrors ClosureCalendar/
│       ├── StaffScheduleResult.cs                  # failure enum + result records
│       ├── CreateStaffScheduleCommand.cs
│       ├── UpdateStaffScheduleCommand.cs
│       ├── DeleteStaffScheduleCommand.cs
│       ├── MarkAbsenceCommand.cs
│       ├── CopyWeekCommand.cs
│       ├── ListStaffScheduleQuery.cs                # director week view
│       ├── GetProjectedOnDutyQuery.cs                # planning-only count
│       └── GetMyScheduleQuery.cs                     # FR-012, StaffOrDirector
├── ChildCare.Contracts/
│   ├── Requests/
│   │   └── StaffScheduleRequests.cs
│   └── Responses/
│       └── StaffScheduleResponses.cs
├── ChildCare.Infrastructure/
│   └── Persistence/
│       ├── TenantDbContext.cs                       # +modelBuilder config, unique index
│       └── Migrations/                               # new EF migration
├── ChildCare.Api/
│   └── Endpoints/
│       └── StaffScheduleEndpoints.cs                 # new, mirrors ClosureCalendarEndpoints.cs
└── ChildCare.Api.Tests/
    └── StaffScheduling/
        ├── StaffScheduleEndpointsTests.cs
        ├── CopyWeekTests.cs
        └── BkrDecouplingTests.cs                          # proves live BKR is unaffected (research.md R1)

web/
├── app/(app)/scheduling/
│   └── page.tsx                                      # new — week-grid rota builder, data loading/actions
├── components/
│   ├── SchedulingGrid.tsx                             # new — week × staff grid
│   └── ScheduleEntryDialog.tsx                        # new — create/edit form
├── lib/generated/api-types.ts                        # regenerated (openapi-typescript)
├── components/Sidebar.tsx                             # +Scheduling nav entry
└── i18n/locales/{en,fr,nl}.json                        # +scheduling.* keys
```

**Structure Decision**: Standard web-application layout already established by this
monorepo — backend feature code follows the `ClosureCalendar`/`Attendance` module precedent
(one `Application/<Feature>/` folder, one `Endpoints.cs` file, entities in
`Domain/Entities`), web feature code adds one route under `web/app/(app)/`. No new
projects, no new top-level directories.

## Complexity Tracking

*No Constitution Check violations — table not needed.*
