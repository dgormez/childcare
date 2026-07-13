# Implementation Plan: Configurable Caregiver PIN

**Branch**: `008b-configurable-caregiver-pin` | **Date**: 2026-07-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008b-configurable-caregiver-pin/spec.md`

## Summary

Add a per-location `RequiresCaregiverPin` setting (default `true`) so a director can make PIN
verification optional at check-in/check-out while keeping the tap-to-identify step mandatory.
Enforcement is server-side: `CheckInCommand`/`CheckOutCommand` skip `VerifyPinCommand.VerifyAsync`
when the location's setting is off, still writing an identical `RoomShift` row either way, which
is what keeps BKR ratio and event/incident attribution unaffected. The roster endpoint surfaces
the flag to the tablet so it can skip rendering the PIN keypad; a new "Inchecken" tab on the
existing `/locations/[id]` web screen lets a director toggle it with explanatory tradeoff copy.
`confirmAdministrator` (medical/sensitive-action confirmation) is explicitly unaffected — it
always requires PIN verification (or its existing Skip), per the resolved clarification.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript (web: Next.js 15, mobile: Expo/React
Native)

**Primary Dependencies**: MediatR, FluentValidation, EF Core 9, BCrypt.Net (existing PIN hashing,
unchanged); openapi-typescript + openapi-fetch (generated clients, both `web/` and `mobile/`)

**Storage**: PostgreSQL 16 (Neon), schema-per-tenant — one new column on `locations`

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend), Vitest + Testing Library
(web), Jest + Testing Library (mobile) — matches constitution Principle V and existing per-app
conventions

**Target Platform**: ASP.NET Core Minimal API (Cloud Run), Next.js web admin, Expo caregiver
tablet app (landscape)

**Project Type**: Web application (backend + web + mobile, existing monorepo structure — no new
projects)

**Performance Goals**: No new performance target; adds one boolean column read to an existing
per-request query path (roster, check-in, check-out) — negligible cost

**Constraints**: Must not change BKR ratio or event/incident attribution behavior in any way
(Constitution Principle II); must not weaken PIN security when the setting is on; existing PINs
must survive toggling the setting in either direction (FR-009)

**Scale/Scope**: One new `Location` column, one new web tab/component, one changed roster
response shape, two changed command handlers (CheckIn/CheckOut) — no new tables, no new screens
beyond the settings tab

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment |
|---|---|
| I. Multi-Tenant Isolation | **Pass.** `RequiresCaregiverPin` lives on the existing tenant-scoped `Location` entity; no new cross-tenant surface. |
| II. Regulatory Compliance by Design | **Pass.** BKR ratio and attribution logic are explicitly unchanged (both already read only `RoomShift.CheckedOutAt`/`StaffProfileId`, never PIN-verification status) — this feature's entire design goal is to leave that enforcement untouched regardless of the new setting. Verified by dedicated parity tests (User Story 3). |
| III. CQRS via MediatR & Thin Endpoints | **Pass.** New endpoint (`checkin-settings`) follows the existing `UpdateLocation*SettingsCommand` MediatR pattern (013f precedent); `CheckInCommandHandler`/`CheckOutCommandHandler` gain a branch, not new business logic in an endpoint file. |
| IV. Internationalization First | **Pass.** New toggle label and tradeoff copy ship as locale keys (NL/FR/EN) from the start, per FR-014. |
| V. Test with Real Infrastructure | **Pass.** Backend tests for both PIN-required and PIN-off paths, plus BKR/attribution parity tests, run against TestContainers PostgreSQL, matching every prior feature. |
| VI. Secure Configuration & Storage | **Pass.** The new migration is authored/reviewed as normal code and rolled out to existing tenant schemas via the existing manual `migrate-tenants` process (no auto-apply) — no carve-out needed, this is a plain schema-changing feature like 004/013f. |
| VII. Monolith-First Simplicity | **Pass.** No new project, no new service — extends existing `Location`/`RoomShifts`/`locations` surfaces in the existing five projects. |

No violations. Complexity Tracking section not needed.

## Project Structure

### Documentation (this feature)

```text
specs/008b-configurable-caregiver-pin/
├── plan.md              # This file
├── research.md           # Phase 0 output
├── data-model.md          # Phase 1 output
├── quickstart.md          # Phase 1 output
├── contracts/
│   └── api.md             # Phase 1 output
└── tasks.md               # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/Entities/Location.cs                              # + RequiresCaregiverPin
├── ChildCare.Application/
│   ├── Locations/UpdateLocationCheckInSettingsCommand.cs               # new
│   ├── RoomShifts/CheckInCommand.cs                                    # + PIN-skip branch
│   ├── RoomShifts/CheckOutCommand.cs                                   # + PIN-skip branch
│   └── RoomShifts/GetRoomRosterQuery.cs                                # + RequiresCaregiverPin in response
├── ChildCare.Contracts/
│   ├── Requests/LocationRequests.cs                                    # + UpdateLocationCheckInSettingsRequest
│   ├── Responses/LocationResponse.cs                                   # + RequiresCaregiverPin field
│   └── Responses/RoomShiftResponses.cs                                 # + RoomRosterResponse wrapper
├── ChildCare.Api/Endpoints/
│   ├── LocationEndpoints.cs                                            # + PUT /{id}/checkin-settings
│   └── RoomShiftEndpoints.cs                                           # CheckInRequest/CheckOutRequest.Pin → nullable
└── ChildCare.Infrastructure/Persistence/Migrations/Tenant/
    └── <timestamp>_AddLocationRequiresCaregiverPin.cs                  # new migration

web/
├── app/(app)/locations/[id]/page.tsx                                   # + "Inchecken" tab entry
├── components/CheckInSettingsForm.tsx                                  # new
└── lib/generated/api-types.ts                                          # regenerated

mobile/
├── app/(room)/index.tsx                                                # skip PinKeypad when off
├── services/roomShift.ts                                               # getRoster/checkIn/checkOut signature updates
└── services/generated/api-types.ts                                     # regenerated
```

**Structure Decision**: No new top-level projects. Changes extend the existing
`backend/`/`web/`/`mobile/` structure established since features 004/007a/008a/013f, following
each app's existing per-feature file-placement conventions.

## Complexity Tracking

*No violations — section not applicable.*
