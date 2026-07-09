# Implementation Plan: Daily Attendance Registration

**Branch**: `010-attendance` | **Date**: 2026-07-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/010-attendance/spec.md`

## Summary

Build the `attendance_records` table and API (one row per child per location per day) plus the
caregiver-tablet one-tap check-in/check-out UI on top of feature 008/008a's existing scaffold,
auth, offline queue, and sync engine. Reuses feature 008a's `IShiftAttributionService` (already
built anticipating this feature, per its own doc comment) to populate `recorded_by`, and its
`RoomShift` roster query to compute the live BKR indicator. Adds an `attendance_record`
entity-type handler to the existing sync engine (feature 008) with a server-wins conflict policy
(a genuine deviation from feature 009's "all writes preserved" policy, required because a
duplicate check-in is a real conflict, not an independent event). `planned_duration_minutes` is
derived once at check-in time from the child's active `Contract` at that location. BKR "nap time"
is inferred from open `sleep` child-events (feature 009) rather than a manual toggle. The
leefgroep (18-cap) BKR regime is out of scope, per a new constitution carve-out (v1.3.0) — no
group-type distinction exists in the data model to enforce it against.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / React Native + Expo (mobile, Expo
Router, NativeWind)

**Primary Dependencies**: MediatR (commands/queries), FluentValidation, EF Core 9 (Npgsql),
expo-sqlite (offline queue, already wired by feature 008), feature 008a's
`IShiftAttributionService` / `RoomShift` roster query (reused, not reimplemented), feature 009's
`BelgianCalendarDay` (reused for the attendance "date" boundary)

**Storage**: PostgreSQL 16, tenant schema — new `attendance_records` table only; no changes to
existing tables

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, per constitution Principle
V, following `ChildCare.Api.Tests`'s `OrganisationOnboardingWebAppFactory` pattern), Jest (mobile,
existing `__tests__/` conventions)

**Target Platform**: Caregiver Expo app (tablet, landscape) for check-in/BKR UI; director web
(Next.js) for the correction/history screen; ASP.NET Core Minimal API (Cloud Run) for the
backend

**Project Type**: Mixed — API-backend capability + caregiver-tablet UI + a secondary director-web
correction screen (per spec.md Product Context)

**Performance Goals**: Check-in/out perceived as instant (optimistic local UI, per SC-001); BKR
read is a cheap per-location query (present-count + `RoomShift` roster, both already indexed by
location) suitable for frequent polling from the tablet

**Constraints**: Fully offline-capable check-in/out/absence (reuses feature 008's offline
queue/sync engine); device-token-authenticated tablet writes (feature 008a's security boundary),
no per-caregiver auth on that route family; server-wins conflict policy (409 on duplicate,
distinct from feature 009's policy); same-day caregiver corrections / any-day director corrections
(feature 009's precedent)

**Scale/Scope**: One new domain concept (`AttendanceRecord`), 1 new table, ~6 new endpoints
(check-in/check-out/absence/correction/list/BKR-read), 1 new mobile sync handler, new check-in UI
on the existing group view, one new director-web screen (attendance correction/history)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Multi-Tenant Isolation (NON-NEGOTIABLE)**: PASS. `attendance_records` lives in
  `TenantDbContext` like every other domain entity since feature 002; no shared/public-schema
  table involved. No carve-out needed or claimed.
- **II. Regulatory Compliance by Design (NON-NEGOTIABLE)**: PASS, under the new v1.3.0 carve-out.
  The three enforceable BKR thresholds (solo max 8; 2+ caregivers max 9/caregiver; nap time max
  14) are computed server-side in the Application layer, not client-side only. The leefgroep
  18-cap is explicitly not implemented — covered by the constitution's new "Carve-out (leefgroep
  ratio, pending group-type data model)" clause added as part of this feature's planning, since no
  feature to date gives `Group`/`Location` a way to be flagged as a leefgroep. Per Phase 1 scope
  (BACKLOG.md), BKR is warning-only, never a hard block on check-in — consistent with the spec.
- **III. CQRS via MediatR & Thin Endpoints**: PASS. Every write (check-in, check-out, absence,
  director correction) is a MediatR command; the BKR read and attendance list/history are MediatR
  queries; `AttendanceEndpoints.cs` maps HTTP to MediatR only.
- **IV. Internationalization First (NON-NEGOTIABLE)**: PASS. All mobile-facing strings (check-in
  labels, absence dialog, BKR status labels) go through `i18n/locales/{nl,fr,en}`; new
  director-web strings go through `next-intl`; API error responses return locale keys
  (`errors.attendance.*`), never raw text.
- **V. Test with Real Infrastructure (NON-NEGOTIABLE)**: PASS. Backend integration tests run
  against TestContainers PostgreSQL per existing `ChildCare.Api.Tests` setup; coverage targets
  happy path + unique-constraint/conflict handling + BKR threshold boundaries + contract-derived
  `planned_duration_minutes` + same-day/any-day correction authorization.
- **VI. Secure Configuration & Storage**: PASS. No new secrets, no new file storage. Errors return
  localized messages; full errors logged server-side only.
- **VII. Monolith-First Simplicity**: PASS. No new project/service; all work lands in the existing
  five-project backend solution, the existing Expo caregiver app, and the existing Next.js web
  admin app.

No unresolved violations. Complexity Tracking table is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/010-attendance/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── attendance-api.md
└── tasks.md             # Phase 2 output (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   └── Entities/
│       └── AttendanceRecord.cs                  # new
├── ChildCare.Application/
│   ├── Attendance/                              # new folder, mirrors ChildEvents/
│   │   ├── CheckInCommand.cs
│   │   ├── CheckOutCommand.cs
│   │   ├── MarkAbsentCommand.cs
│   │   ├── CorrectAttendanceRecordCommand.cs    # director-only / device-or-director edit
│   │   ├── DeleteAttendanceRecordCommand.cs
│   │   ├── ListAttendanceQuery.cs               # director web: history/corrections view
│   │   ├── GetBkrRatioQuery.cs
│   │   ├── PlannedDurationCalculator.cs         # contract-weekday → minutes, null when no match
│   │   └── AttendanceEditWindowPolicy.cs        # same-day caregiver / any-day director rule
│   └── RoomShifts/
│       └── IShiftAttributionService.cs          # existing — reused, not modified
├── ChildCare.Contracts/
│   ├── Requests/AttendanceRequests.cs
│   └── Responses/AttendanceResponses.cs
├── ChildCare.Infrastructure/
│   └── Persistence/TenantDbContext.cs           # add DbSet<AttendanceRecord>, uuid[] mapping
│       # for RecordedBy (mirrors ChildEvent.RecordedBy's existing uuid[] column-type pattern)
└── ChildCare.Api/
    └── Endpoints/AttendanceEndpoints.cs         # new — DeviceAuthenticated + DeviceOrDirector
                                                   # groups, mirrors ChildEventEndpoints.cs

mobile/
├── services/
│   ├── attendance.ts                            # new — API calls + sync-handler registration
│   └── syncEngine.ts                            # register 'attendance_record' entity type with
│                                                  # a server-wins (not "all writes preserved")
│                                                  # conflict handler — the actual code change
│                                                  # here, distinct from feature 009's handler
├── app/(app)/
│   └── index.tsx                                # extended: tap-to-check-in/out + BKR indicator
├── components/
│   ├── BkrIndicator.tsx                         # new
│   └── AbsenceDialog.tsx                        # new — separate deliberate action from check-in
└── i18n/locales/{nl,fr,en}.json                  # new `attendance` key in each flat locale file

web/
├── app/(app)/attendance/
│   └── page.tsx                                 # new — director correction/history screen
└── lib/generated/api-types.ts                    # regenerated (openapi-typescript), committed
```

**Structure Decision**: Follows the established pattern exactly (`ChildCare.Application/<Feature>/`
alongside `ChildCare.Api/Endpoints/<Feature>Endpoints.cs` on the backend; `services/` and
`components/` alongside existing route files on mobile; `app/(app)/<feature>/page.tsx` on web,
per 007a's precedent for director screens) — no new top-level structure introduced.

## Complexity Tracking

> No unresolved violations — table intentionally omitted. The one constitution-level accommodation
> (leefgroep carve-out) is codified as a versioned amendment to `constitution.md` itself (v1.3.0),
> not a plan-level bend, per the governance precedent set by the 1.1.0/1.2.0 amendments.
