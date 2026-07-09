# Implementation Plan: Child Event Timeline

**Branch**: `009-child-events` | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009-child-events/spec.md`

## Summary

Build the `child_events` table and API (single JSONB-backed table per constitution's
Development Workflow section, one row per recorded occurrence across 11 event types) plus the
caregiver-tablet quick-entry UI on top of feature 008/008a's existing scaffold, auth, offline
queue, and sync engine. Reuses feature 008a's `IShiftAttributionService` (already built
anticipating this feature) to resolve `recorded_by` from the room's shift log, and its
select-then-PIN `/room-shifts/confirm-administrator` endpoint for medication/temperature
administrator attribution. Adds a `child_event` entity-type handler to the existing sync engine
(feature 008), with a create/end-merge rule for in-progress sleep events. Daily summary is
computed at query time (no materialized view). Photo attachment (originally listed) is deferred
per the 2026-07-08 clarification session.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript / React Native + Expo (mobile,
Expo Router, NativeWind)

**Primary Dependencies**: MediatR (commands/queries), FluentValidation, EF Core 9 (Npgsql),
expo-sqlite (offline queue, already wired by feature 008), expo-notifications (already a
dependency, not yet integrated server-side)

**Storage**: PostgreSQL 16, tenant schema — new `child_events` table; no changes to existing
tables

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, per constitution Principle
V), Jest (mobile, existing `__tests__/` conventions)

**Target Platform**: Caregiver Expo app (tablet, landscape) for entry UI; ASP.NET Core Minimal
API (Cloud Run) for the backend; no parent-facing or web-admin UI in this feature

**Project Type**: Mixed — API-backend capability + caregiver-tablet UI (per spec.md Product
Context)

**Performance Goals**: Routine event save perceived as instant (optimistic local UI, per SC-001,
5s human-workflow target, not a server-latency target); event list/timeline queries paginated
from the start (no full-history load, SC-006)

**Constraints**: Fully offline-capable create/read for events (reuses feature 008's offline
queue/sync engine, no new offline mechanism); device-token-authenticated tablet, no per-event
user auth (feature 008a's security boundary); same-day edit window enforced server-side

**Scale/Scope**: One new domain concept (Child Event) across 11 fixed event types, 1 new table,
~6 new endpoints (create/list/get/update/delete/daily-summary), 1 new mobile sync handler, new
quick-action-sheet + timeline UI on the existing child detail screen

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Multi-Tenant Isolation (NON-NEGOTIABLE)**: PASS. `child_events` lives in `TenantDbContext`
  like every other domain entity since feature 002; no shared/public-schema table involved; all
  reads/writes go through the request's resolved tenant schema. No carve-out needed or claimed.
- **II. Regulatory Compliance by Design**: PASS. Not a BKR/day-overlap/closure-calendar feature;
  the one regulatory-adjacent requirement (weight tracking is legally required in Belgian KDVs)
  is satisfied by the `weight` event type existing and being recordable — no additional
  server-side enforcement is implied by the spec (there's no "weight must be recorded monthly"
  requirement in scope).
- **III. CQRS via MediatR & Thin Endpoints**: PASS. Every write (create/update/delete event,
  confirm administrator — reused from 008a) is a MediatR command; the daily summary and
  paginated list are MediatR queries; `ChildEventEndpoints.cs` maps HTTP to MediatR only.
- **IV. Internationalization First (NON-NEGOTIABLE)**: PASS. All mobile-facing strings (quick
  action labels, pending-sync badge, empty states, error keys) go through
  `i18n/locales/{nl,fr,en}`; API error responses return locale keys per existing convention
  (`errors.*`), never raw text.
- **V. Test with Real Infrastructure (NON-NEGOTIABLE)**: PASS. Backend integration tests run
  against TestContainers PostgreSQL per existing `ChildCare.Api.Tests` setup; coverage targets
  happy path + same-day-edit-window enforcement + temperature-threshold trigger +
  visible_to_parent filtering + offline sync-merge behavior (spec's Technical Requirements).
- **VI. Secure Configuration & Storage**: PASS. No new secrets. No new file storage (photo
  attachment deferred, so no GCS signed-URL surface added by this feature). Errors return
  localized messages; full errors logged server-side only.
- **VII. Monolith-First Simplicity**: PASS. No new project/service; all work lands in the
  existing five-project solution and the existing Expo caregiver app.

No violations. Complexity Tracking table is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/009-child-events/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md         # Phase 1 output
├── contracts/
│   └── child-events-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Domain/
│   └── Entities/
│       └── ChildEvent.cs                       # new
├── ChildCare.Application/
│   ├── Common/
│   │   └── BelgianCalendarDay.cs               # new — shared Europe/Brussels day-boundary helper
│   └── ChildEvents/                            # new folder, mirrors Contracts/RoomShifts
│       ├── RecordChildEventCommand.cs
│       ├── UpdateChildEventCommand.cs
│       ├── DeleteChildEventCommand.cs
│       ├── ListChildEventsQuery.cs
│       ├── GetDailySummaryQuery.cs
│       ├── ChildEventPayloadValidator.cs        # per-event-type payload shape validation
│       ├── ChildEventEditWindowPolicy.cs        # same-day / director-any-day rule
│       ├── ITemperatureAlertService.cs
│       └── TemperatureAlertService.cs           # Expo push dispatch, server-side only
├── ChildCare.Contracts/
│   ├── Requests/ChildEventRequests.cs
│   └── Responses/ChildEventResponses.cs
├── ChildCare.Infrastructure/
│   └── Persistence/TenantDbContext.cs           # add DbSet<ChildEvent>, jsonb mapping
│   └── Push/ExpoPushSender.cs                   # new — thin Expo Push Notification client
└── ChildCare.Api/
    └── Endpoints/ChildEventEndpoints.cs         # new

mobile/
├── services/
│   ├── childEvents.ts                           # new — API calls + sync-handler registration
│   └── syncEngine.ts                            # two small fixes: replay() re-reads current
│                                                 # payload before send (R3/CHK008), and a 400
│                                                 # response is treated as permanent, not transient
│                                                 # (FR-014a, analyze finding C1)
├── app/(app)/
│   ├── index.tsx                                # unchanged (entry point already exists)
│   └── child/[id].tsx                           # extended: timeline + quick-action sheet
├── components/
│   ├── QuickActionSheet.tsx                     # new
│   └── EventTimeline.tsx                        # new
└── i18n/locales/{nl,fr,en}.json                  # new `childEvents` key in each flat locale file
```

**Structure Decision**: Follows the established pattern exactly (`ChildCare.Application/<Feature>/`
alongside `ChildCare.Api/Endpoints/<Feature>Endpoints.cs` on the backend; `services/` and
`components/` alongside existing route files on mobile) — no new top-level structure introduced.

## Complexity Tracking

> No violations — table intentionally omitted.
