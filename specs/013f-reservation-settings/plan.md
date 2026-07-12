# Implementation Plan: Reservation Settings

**Branch**: `013f-reservation-settings` | **Date**: 2026-07-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/013f-reservation-settings/spec.md`

## Summary

Add four per-location settings (`reservation_absences_mode`, `reservation_extras_mode`,
`reservation_swaps_mode`, `reservation_notice_hours`) to feature 004's `Location` entity, giving
directors control over whether each of 013a's three day-reservation types is `disabled`,
`informational` (auto-approved, no director action), or `approval` (013a's existing behavior). A
new `ReservationPolicyResolver` computes the effective policy per submission from the child's
active contracts (research.md R3), since `DayReservation` deliberately has no `LocationId`
(013a research.md R7). `SubmitDayReservationCommandHandler` enforces the resolved policy: reject
`disabled` types (403), reject submissions inside the notice-hours window (422), and auto-approve
`informational` submissions with the exact same downstream effects an `approval`-mode decision
would have (attendance pre-registration for `absence`, per this feature's Clarifications session).
A new `PUT /api/locations/{id}/reservation-settings` endpoint mirrors 011's
`ConfirmExistingAttendance` pattern to warn (409) before a mode change strands pending requests,
unless confirmed. Ships the first real web `/locations` screen (007a's placeholder is replaced,
research.md R5) hosting a "Reserveringsinstellingen" tab, plus parent-mobile entry-point hiding
and inline blocking for `disabled` types.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript (web: Next.js 15 App Router;
parent-mobile: Expo/React Native)

**Primary Dependencies**: ASP.NET Core Minimal APIs, MediatR, FluentValidation, EF Core 9
(backend); Next.js 15 + Tailwind + shadcn/ui (web); Expo Router + NativeWind (parent-mobile);
openapi-typescript + openapi-fetch (both frontends)

**Storage**: PostgreSQL 16, schema-per-tenant (four new columns on the existing `locations` table)

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, constitution Principle V);
Vitest + `@testing-library/react` (web, jsdom, feature 007a precedent);
`@testing-library/react-native` (parent-mobile, feature 013a precedent)

**Target Platform**: Cloud Run (backend API); web browser ≥1280px (director); iOS/Android via
Expo (parent)

**Project Type**: Web application (backend + two frontends) — existing monorepo structure

**Performance Goals**: Standard interactive-app expectations; settings reads/writes are simple
single-row lookups, no special throughput target

**Constraints**: All mode/notice-hours enforcement happens server-side in the command handler
(constitution Principle II spirit — never client-only, even though this isn't a BKR/named
regulatory rule); the parent app hiding a disabled entry point is a UX nicety, never the actual
enforcement boundary (FR-007)

**Scale/Scope**: Four new columns on one existing table, one new endpoint, one new resolver
service, one new web screen (list + two-tab settings panel) replacing a placeholder, small
additions to three existing parent-mobile screens

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation | PASS | All new columns live on the existing tenant-schema `Location` entity; no new cross-tenant read path. |
| II. Regulatory Compliance by Design | PASS | Not a BKR/overlap/closure rule. The one enforcement rule this feature owns (disabled-type rejection, notice-hours) is enforced server-side in `SubmitDayReservationCommandHandler`, never client-only — consistent with the principle's spirit, same reasoning 013a's own plan used for its closure-day/past-date checks. |
| III. CQRS via MediatR & Thin Endpoints | PASS | `UpdateLocationReservationSettingsCommand` is a MediatR command with FluentValidation; `SubmitDayReservationCommandHandler`'s new enforcement lives in the existing handler via an injected `ReservationPolicyResolver` service, not in `LocationEndpoints.cs`/`DayReservationEndpoints.cs`, which only map HTTP ↔ MediatR. |
| IV. Internationalization First | PASS | All new web/parent-mobile strings via i18n keys (next-intl / react-i18next); new API error responses return `errorKey` values (`errors.day_reservations.request_type_disabled`, `errors.day_reservations.notice_period_required`, `errors.location.reservation_settings.pending_requests_warning`). |
| V. Test with Real Infrastructure | PASS | Backend integration tests run against TestContainers PostgreSQL, per every feature since 003. |
| VI. Secure Configuration & Storage | PASS | No secrets, no file storage; a normal reviewed EF Core migration adds four columns with defaults. |
| VII. Monolith-First Simplicity | PASS | No new project, no new service. Extends the existing 5-project backend solution + existing `web`/`parent-mobile` apps. |

No violations. Complexity Tracking section not needed.

## Project Structure

### Documentation (this feature)

```text
specs/013f-reservation-settings/
├── plan.md                              # This file
├── research.md                          # Phase 0 output
├── data-model.md                        # Phase 1 output
├── quickstart.md                        # Phase 1 output
├── contracts/
│   └── reservation-settings-api.md      # Phase 1 output
└── tasks.md                             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/Location.cs                         # + 4 fields
│   └── Enums/ReservationRequestMode.cs               # new
├── ChildCare.Application/
│   ├── Locations/
│   │   ├── UpdateLocationReservationSettingsCommand.cs        # new
│   │   ├── UpdateLocationReservationSettingsCommandHandler.cs # new
│   │   ├── UpdateLocationReservationSettingsCommandValidator.cs # new
│   │   ├── LocationMapper.cs                          # + 4 fields
│   │   └── LocationResult.cs                          # + PendingRequestsWarning-shaped failure
│   └── DayReservations/
│       ├── ReservationPolicyResolver.cs               # new (research.md R3)
│       ├── SubmitDayReservationCommand.cs              # handler extended: policy check + auto-approve path
│       ├── GetReservationAvailabilityQuery.cs          # new — parent-facing read (contracts)
│       └── DayReservationResult.cs                     # + RequestTypeDisabled, NoticePeriodRequired failures
├── ChildCare.Contracts/
│   ├── Requests/LocationRequests.cs                    # + UpdateLocationReservationSettingsRequest
│   └── Responses/LocationResponse.cs                   # + 4 fields
├── ChildCare.Api/Endpoints/
│   ├── LocationEndpoints.cs                            # + PUT .../reservation-settings
│   └── DayReservationEndpoints.cs                      # + 2 new MapFailure cases
├── ChildCare.Infrastructure/Persistence/
│   ├── TenantDbContext.cs                              # Location entity config extended (enum-as-text, inline per existing convention)
│   └── Migrations/Tenant/<timestamp>_AddReservationSettings.cs
└── ChildCare.Api.Tests/
    ├── LocationReservationSettingsTests.cs              # new
    └── DayReservations/DayReservationEndpointsTests.cs   # extended

web/
├── app/(app)/locations/
│   ├── page.tsx                          # replaces NotYetAvailable — real list
│   └── [id]/page.tsx                     # settings panel: Algemeen + Reserveringsinstellingen tabs
├── components/
│   ├── LocationsTable.tsx                # new
│   ├── ReservationSettingsForm.tsx       # new
│   ├── PendingRequestsWarningDialog.tsx  # new (mirrors ApproveDayReservationDialog's confirm pattern)
│   └── DayReservationsTable.tsx          # extended: "auto-approved" badge when decidedBy is null
├── components/Sidebar.tsx                # "locations" moves from PLACEHOLDER_NAV to REAL_NAV
└── lib/generated/api-types.ts            # regenerated, committed per 007a precedent

parent-mobile/
├── app/(app)/index.tsx                   # quick-action buttons conditionally rendered
├── app/(app)/requests/{absence,extra,exchange}.tsx  # per-child disabled-type inline block
├── services/locations.ts                 # new — fetch effective per-child request-type availability
└── services/generated/api-types.ts       # regenerated, committed per 013a precedent
```

**Structure Decision**: Follows the existing three-surface monorepo layout exactly — no new
projects. `ReservationPolicyResolver` lives under `DayReservations/` (not `Locations/`) since its
only caller is `SubmitDayReservationCommandHandler` and its logic is about day-reservation
enforcement, not location administration. Web gets a new `locations/[id]` detail route (App
Router dynamic segment) rather than a modal/dialog, since it hosts two tabs of real form content —
consistent with `reference-products.md`'s "avoid hidden actions, prefer a real route" guidance
already applied in 007a (`NotYetAvailable` → its own route, not a dialog).

## Complexity Tracking

*No violations — table not needed.*
