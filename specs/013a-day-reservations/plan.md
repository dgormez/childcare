# Implementation Plan: Day Reservations (Parent Requests + Director Approval Queue)

**Branch**: `013a-day-reservations` | **Date**: 2026-07-11 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/013a-day-reservations/spec.md`

## Summary

Add a `day_reservations` tenant table plus MediatR commands/queries and Minimal API endpoints
letting a parent submit absence/extra/exchange day requests for their linked children, and
letting a director see a single newest-first "Verzoeken" queue and approve/reject each request in
one action. Approving an `absence` request writes a pre-registered absence into the existing
attendance system (feature 010's `AttendanceRecord`, reusing its closure-day guard). Approving an
`extra` or `exchange` request only updates the reservation's own status — the day itself is picked
up naturally by feature 010's existing "extra day, no matching contracted day" check-in path
(`PlannedDurationMinutes = null`), with feature 014 (invoicing, not yet built) expected to read
approved `extra`/`exchange` reservations once it exists. Status changes push an Expo notification
to the parent via the existing `IExpoPushSender` port. New UI: three parent-mobile entry points
(`parent-mobile/`) and one director-web "Verzoeken" screen (`web/`).

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript (web: Next.js 15 App Router; parent
mobile: Expo/React Native)

**Primary Dependencies**: ASP.NET Core Minimal APIs, MediatR, FluentValidation, EF Core 9
(backend); Next.js 15 + Tailwind + shadcn/ui (web); Expo Router + NativeWind (parent-mobile);
openapi-typescript + openapi-fetch (both frontends)

**Storage**: PostgreSQL 16, schema-per-tenant (new `day_reservations` table in `TenantDbContext`)

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, per constitution Principle
V); Vitest + `@testing-library/react` (web, jsdom, per feature 007a precedent);
`@testing-library/react-native` (parent-mobile, per feature 013 precedent)

**Target Platform**: Cloud Run (backend API); web browser ≥1280px (director); iOS/Android via
Expo (parent)

**Project Type**: Web application (backend + two frontends) — existing monorepo structure

**Performance Goals**: Standard interactive-app expectations; no special throughput target — the
approval queue is director-facing, low request volume per tenant per day (tens, not thousands)

**Constraints**: All validation (past-date, contracted-day match, closure-day) happens
server-side in the command handler (constitution Principle II — never client-only); parent
authorization scoped via `ICurrentParentContactResolver` (feature 013 precedent); no offline
queue needed (per spec.md's Assumptions — this is a live-connectivity request/approval flow,
unlike caregiver-tablet event logging)

**Scale/Scope**: Single new tenant table, ~6 new endpoints, 2 new frontend screens (1 web, 3
parent-mobile entry points sharing 1 form component)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation | PASS | `DayReservation` lives in `TenantDbContext`, resolved via `TenantMiddleware` like every entity since 002. No cross-tenant read path introduced. |
| II. Regulatory Compliance by Design | PASS | Not a BKR/ratio feature. The one regulatory-adjacent rule this feature owns (closure-day blocking, past-date blocking, contracted-day validation) is enforced server-side in command/handler validators, never client-only — consistent with the principle's spirit even though it isn't one of the principle's named BKR/overlap/closure rules itself. Reuses `IClosureCalendarReader`, the same closure-day guard 010/011 already enforce. |
| III. CQRS via MediatR & Thin Endpoints | PASS | All writes (submit/cancel/approve/reject) are MediatR commands with FluentValidation; the queue list is a MediatR query; `DayReservationEndpoints.cs` only maps HTTP ↔ MediatR, per the `WaitingListEndpoints.cs` precedent. |
| IV. Internationalization First | PASS | All parent-mobile/web strings via i18n keys (next-intl / react-i18next); API error responses return `errorKey` values, per `WaitingListEndpoints.cs`'s `MapFailure` pattern. |
| V. Test with Real Infrastructure | PASS | Backend integration tests run against TestContainers PostgreSQL, per every feature since 003. |
| VI. Secure Configuration & Storage | PASS | No secrets, no file storage, no migration auto-apply — a normal reviewed EF Core migration. |
| VII. Monolith-First Simplicity | PASS | No new project, no new service. Adds to the existing 5-project solution + existing `web`/`parent-mobile` apps. |

No violations. Complexity Tracking section not needed.

## Project Structure

### Documentation (this feature)

```text
specs/013a-day-reservations/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/            # Phase 1 output
│   └── day-reservations-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   ├── Entities/DayReservation.cs
│   └── Enums/DayReservationType.cs, DayReservationStatus.cs
├── ChildCare.Application/DayReservations/
│   ├── SubmitDayReservationCommand.cs
│   ├── CancelDayReservationCommand.cs
│   ├── ApproveDayReservationCommand.cs
│   ├── RejectDayReservationCommand.cs
│   ├── ListPendingDayReservationsQuery.cs   (director queue)
│   ├── ListMyDayReservationsQuery.cs        (parent history — FR-019)
│   ├── DayReservationMapper.cs
│   └── DayReservationResult.cs
├── ChildCare.Contracts/
│   ├── Requests/DayReservationRequests.cs
│   └── Responses/DayReservationResponses.cs
├── ChildCare.Api/Endpoints/DayReservationEndpoints.cs
├── ChildCare.Infrastructure/Persistence/
│   ├── Configurations/Tenant/DayReservationConfiguration.cs
│   └── Migrations/Tenant/<timestamp>_AddDayReservations.cs
└── ChildCare.Api.Tests/DayReservationTests.cs

web/
├── app/(app)/requests/page.tsx      # "Verzoeken" queue
├── lib/generated/api-types.ts       # regenerated, committed per 007a precedent
└── (component additions under app/(app)/requests/ as needed by tasks.md)

parent-mobile/
├── app/(app)/requests/
│   ├── absence.tsx        # "Mijn kind is ziek"
│   ├── extra.tsx           # "Extra dag aanvragen"
│   ├── exchange.tsx        # "Dagwissel aanvragen"
│   └── index.tsx            # own-request history (FR-019)
├── services/dayReservations.ts
└── services/generated/api-types.ts  # regenerated, committed per 013 precedent
```

**Structure Decision**: Follows the existing three-surface monorepo layout exactly — no new
projects. Backend feature folder named `DayReservations` (English identifier per constitution
Principle IV, even though the Dutch UI labels "Verzoeken"/"Dagwissel" appear only in translation
resources). Web gets one new route (`/requests`) replacing 007a's `NotYetAvailable` placeholder
pattern is not applicable here since `requests` has no existing placeholder route — it's a new
sidebar entry. Parent-mobile gets a new `requests/` route group with three thin entry screens
sharing one form component (single component parameterized by type, to avoid triplicating the
date-picker + reason-field form per FR-018).

## Complexity Tracking

*No violations — table not needed.*
