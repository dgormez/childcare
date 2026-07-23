# Implementation Plan: Staff App (Personal Rota & Leave)

**Branch**: `027-staff-app` | **Date**: 2026-07-22 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/027-staff-app/spec.md`

## Summary

Extend feature 012's `StaffSchedule` entity in place with publish/draft state, a `Status`
enum (reconciling the existing `IsAbsent`/`AbsenceReason` fields into one representation),
`CoverStaffId`, `Notes`, and `CreatedBy`; add `StaffProfile.ContractedDays` (feature 005); add
a new `StaffLeaveRequest` entity. Extend the director-web scheduling grid (012) with
contracted-day/closure-day greying, publish/unpublish, an on-the-fly sick-cover assignment
flow, and a leave-request approval queue. Build a brand-new Expo project, `staff-mobile`
(personal phone, distinct from the shared caregiver-tablet `mobile` app), consuming the
already-built-but-unconsumed `GET /api/staff-schedules/me` endpoint (extended to respect
publish state) for schedule viewing, plus new endpoints for sick-report and leave-request
submission. Push notifications reuse feature 014a's `IExpoPushSender`/`Notification` pattern
with three new `NotificationType` values.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / Next.js 15 App Router (web admin
extensions), TypeScript / React Native (Expo, new `staff-mobile` project)

**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core 9, MediatR, FluentValidation
(backend); Tailwind, shadcn/ui, openapi-fetch, next-intl (web); Expo Router, NativeWind,
`lucide-react-native`, `expo-notifications`, `expo-secure-store`, i18next/`expo-localization`,
openapi-fetch, Zustand (new `staff-mobile`, mirroring `parent-mobile`'s established stack)

**Storage**: PostgreSQL 16, tenant schema — extends `staff_schedules`, adds
`staff_leave_requests`, extends `staff_profiles`

**Testing**: xUnit + Moq + TestContainers-provisioned PostgreSQL (backend, Principle V);
Vitest + `@testing-library/react` (web, jsdom); Jest + `@testing-library/react-native` (new
`staff-mobile`, mirroring `parent-mobile`'s existing setup)

**Target Platform**: Cloud Run (backend API), browser (director web, desktop ≥1280px), iOS/
Android via Expo (new personal staff app, phone/portrait)

**Project Type**: Web application + new mobile app — backend API + Next.js director-web
extensions + a new Expo project (`staff-mobile`)

**Performance Goals**: Staff schedule read is a small, date-bounded (4-week) query per staff
member — no new performance-sensitive path; rota grid remains single-location/single-week scale
(tens of staff), consistent with feature 012's existing goals.

**Constraints**: Schedule/leave-request reads MUST resolve staff identity from the JWT, never a
client-supplied ID (FR-015); assignment/cover writes MUST continue to enforce
`StaffLocationEligibility` (FR-014); `GetBkrRatioQuery` MUST NOT read any new field added to
`StaffSchedule` (FR-016, extends feature 012's existing decoupling test class); cover-assignment
MUST remain race-safe under concurrent requests (reuse `IAdvisoryLockService`, FR-018).

**Scale/Scope**: Same single-tenant scale as feature 012 (tens of staff, 1–3 locations per
organisation in Phase 1); `staff-mobile` is a new, minimal Expo project (schedule view, sick
report, leave request, notifications — 4 screens plus auth, not a large app).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation | PASS | `staff_schedules` (extended) and `staff_leave_requests` (new) both live in the tenant schema, no explicit tenant FK column — same structural pattern as every existing tenant table. All access goes through `TenantDbContext`/`ITenantDbContext`. No new cross-tenant surface. |
| II. Regulatory Compliance by Design (BKR) | PASS | This feature does not implement or modify BKR enforcement. `GetBkrRatioQuery` remains sourced exclusively from `RoomShifts`; FR-016 requires a regression test proving the new `Status`/`CoverStaffId` fields on `StaffSchedule` are never read by it, extending feature 012's `BkrDecouplingTests`. |
| III. CQRS via MediatR & Thin Endpoints | PASS | All writes (publish/unpublish, cover-assign, leave-request create/decide, sick-report) are MediatR commands; schedule/leave reads are MediatR queries; endpoint files map HTTP only. |
| IV. Internationalization First | PASS | All new director-web and `staff-mobile` strings are locale keys (NL/FR/EN) — FR-017. API error responses return `errorKey` values, matching every existing endpoint's pattern. |
| V. Test with Real Infrastructure | PASS | Backend integration tests run against TestContainers PostgreSQL — publish/visibility gating, cover-assignment concurrency, leave-request approval → absence-marking, cross-staff read isolation (FR-015), and the extended BKR-decoupling test. |
| VI. Secure Configuration & Storage | N/A | No secrets, no file storage, no PDF in this feature. Push tokens reuse the existing Expo push mechanism (no new secret class). |
| VII. Monolith-First Simplicity | PASS | No new backend projects — code lives in the existing five-project solution. The one new "project" is `staff-mobile`, a client app, not a backend service; this mirrors `parent-mobile` existing alongside `mobile` and does not add a deployable backend component, so it does not implicate this principle's backend-monolith concern. |

No violations — Complexity Tracking table not needed.

## Project Structure

### Documentation (this feature)

```text
specs/027-staff-app/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── staff-app-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/
│   │   ├── StaffSchedule.cs                        # extended: +Status, CoverStaffId, Notes,
│   │   │                                            #   CreatedBy, IsPublished, PublishedAt
│   │   ├── StaffProfile.cs                          # extended: +ContractedDays
│   │   └── StaffLeaveRequest.cs                     # new
│   └── Enums/
│       ├── StaffScheduleStatus.cs                   # new: Scheduled/Confirmed/Absent/Covered
│       └── StaffLeaveRequestStatus.cs                # new: Pending/Approved/Rejected
│       # StaffLeaveRequest.Type reuses AbsenceReason-shaped values (Sick/Annual/Other) —
│       # see research.md R3 for the exact reconciliation with the existing AbsenceReason enum
├── ChildCare.Application/
│   ├── Common/
│   │   └── ITenantDbContext.cs                      # +DbSet<StaffLeaveRequest>
│   ├── StaffScheduling/                              # existing folder (012), extended
│   │   ├── PublishScheduleWeekCommand.cs             # new
│   │   ├── ReportSickCommand.cs                      # new — FR-005
│   │   ├── AssignCoverCommand.cs                     # new — FR-006/007
│   │   ├── GetSickCoverCandidatesQuery.cs             # new — FR-006
│   │   ├── MarkAbsenceCommand.cs                      # extended: reconciles Status/IsAbsent
│   │   ├── GetMyScheduleQuery.cs                      # extended: filters IsPublished
│   │   └── StaffScheduleNotificationService.cs        # new — publish/change push (mirrors
│   │                                                   #   ClosureNotificationService shape)
│   └── StaffLeaveRequests/                            # new folder, mirrors StaffScheduling/
│       ├── StaffLeaveRequestResult.cs
│       ├── CreateLeaveRequestCommand.cs               # new — FR-009
│       ├── DecideLeaveRequestCommand.cs               # new — FR-010/011
│       ├── ListLeaveRequestsQuery.cs                   # director queue — FR-010
│       ├── GetMyLeaveRequestsQuery.cs                  # staff self-read — FR-012
│       └── StaffLeaveRequestNotificationService.cs     # new — LeaveRequestDecided push
├── ChildCare.Contracts/
│   ├── Requests/
│   │   ├── StaffScheduleRequests.cs                    # extended
│   │   └── StaffLeaveRequestRequests.cs                # new
│   └── Responses/
│       ├── StaffScheduleResponses.cs                   # extended
│       └── StaffLeaveRequestResponses.cs               # new
├── ChildCare.Infrastructure/
│   ├── Persistence/
│   │   ├── TenantDbContext.cs                          # +StaffLeaveRequest config, ContractedDays
│   │   │                                               #   text[] conversion (mirrors DietaryType)
│   │   └── Migrations/Tenant/                          # new EF migration
│   └── Push/                                            # no change — reuses ExpoPushSender as-is
├── ChildCare.Domain/Enums/NotificationType.cs          # +SchedulePublished, AssignmentChanged,
│                                                         #   LeaveRequestDecided
├── ChildCare.Api/
│   └── Endpoints/
│       ├── StaffScheduleEndpoints.cs                    # extended: publish, sick-report, cover
│       └── StaffLeaveRequestEndpoints.cs                # new
└── ChildCare.Api.Tests/
    └── StaffScheduling/
        ├── PublishVisibilityTests.cs                    # new
        ├── SickCoverAssignmentTests.cs                   # new — incl. concurrency
        ├── LeaveRequestApprovalTests.cs                  # new
        ├── CrossStaffIsolationTests.cs                    # new — FR-015
        └── BkrDecouplingTests.cs                          # extended (existing file, feature 012)

web/
├── app/(app)/scheduling/page.tsx                        # extended: publish button, cover prompt
├── app/(app)/leave-requests/page.tsx                     # new — "Verlofaanvragen" queue
├── components/
│   ├── SchedulingGrid.tsx                                # extended: greying, publish state
│   ├── ScheduleEntryDialog.tsx                            # extended: status/notes fields
│   ├── SickCoverDialog.tsx                                 # new — FR-006
│   └── LeaveRequestTable.tsx                                # new
├── components/Sidebar.tsx                                   # +Verlofaanvragen nav entry
├── lib/generated/api-types.ts                                # regenerated (openapi-typescript)
└── i18n/locales/{en,fr,nl}.json                              # +scheduling.*/leaveRequests.* keys

staff-mobile/                                                 # new Expo project, mirrors
│                                                              #   parent-mobile/'s layout
├── app/
│   ├── (auth)/login.tsx
│   └── (app)/
│       ├── schedule/index.tsx                                # week/day toggle — FR-003/004
│       ├── report-sick.tsx                                    # FR-005
│       ├── leave-requests/{index.tsx,new.tsx}                 # FR-009/012
│       └── notifications.tsx                                  # mirrors parent-mobile's screen
├── components/
│   ├── ScheduleDayCard.tsx
│   └── ScheduleWeekList.tsx
├── services/generated/                                        # openapi-fetch client (generated)
├── theme/colors.js                                             # copied token set (design-system.md)
├── i18n/locales/{en,fr,nl}.json
├── hooks/useIsOffline.ts                                       # copied from parent-mobile (013c)
├── store/                                                       # Zustand auth/session store
├── app.config.js / babel.config.js / metro.config.js / tailwind.config.js / tsconfig.json /
│   package.json / jest.config.js                                # scaffolded from parent-mobile
└── __tests__/                                                    # Jest + RTL, mirrors parent-mobile
```

**Structure Decision**: Backend and web-admin changes extend the existing modules established
by feature 012 (`StaffScheduling/` Application folder, `StaffScheduleEndpoints.cs`,
`scheduling/` web route) — no new backend project, per Constitution VII. The one new top-level
directory, `staff-mobile/`, is a client app scaffolded from `parent-mobile/`'s existing Expo
project conventions (theme tokens, i18n, openapi-fetch generation, Jest/RTL test setup), the
same way `parent-mobile/` itself was scaffolded from `mobile/`'s conventions when it shipped.

## Complexity Tracking

*No Constitution Check violations — table not needed.*
