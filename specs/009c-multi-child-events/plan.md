# Implementation Plan: Multi-Child Events

**Branch**: `009c-multi-child-events` | **Date**: 2026-07-11 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/009c-multi-child-events/spec.md`

## Summary

Let a caregiver select multiple present children on the room roster screen and log one event for
all of them in a single submission, creating one independent `ChildEvent` row per selected child
via a new `POST /api/child-events/batch` endpoint with per-child partial-success semantics.
Bundles one prerequisite fix (research.md R2): `GET /api/children`/`GET /api/groups` — the room
roster's own data source — currently reject a pure kiosk device-token session, which this
feature's UI depends on to function at all.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript / React Native (Expo, mobile).

**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core 9, MediatR, FluentValidation
(backend, all existing); `react-i18next`, `lucide-react-native`, existing `offlineQueue`/
`syncEngine` modules (mobile, all existing) — no new dependencies.

**Storage**: PostgreSQL 16, tenant schema. No new tables/migration — reuses `ChildEvent` (009)
and reads `AttendanceRecord` (010).

**Testing**: xUnit + TestContainers-provisioned PostgreSQL (backend, per constitution Principle
V); Jest + React Testing Library (mobile).

**Target Platform**: Caregiver tablet (Expo, landscape/kiosk mode, feature 008a) for the UI;
ASP.NET Core API (Cloud Run) for the batch endpoint.

**Project Type**: Mobile + API (existing monorepo structure — `backend/`, `mobile/`).

**Performance Goals**: No new goal beyond the existing per-request expectations; a 30-child batch
completing in well under a caregiver-noticeable delay (sub-second range) is sufficient — see
research.md R5 for why per-child isolation is prioritized over a single bulk-insert path at this
scale.

**Constraints**: Offline-capable (queued as one entry, replayed as one call, research.md R6);
per-child transactional isolation (research.md R5); max 30 `childIds` per batch, enforced
server-side independent of the client.

**Scale/Scope**: One new backend endpoint + supporting Application-layer command; one prerequisite
auth-policy fix on two existing endpoints; mobile UI changes confined to the room roster screen and
`QuickActionSheet`; no new mobile screens.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Multi-Tenant Isolation** — Pass. The batch endpoint runs through `ITenantDbContext` like
  every other command; no new context type introduced. `GET /api/children`/`/api/groups`'s R2 fix
  changes only which auth scheme is accepted, not tenant resolution (`TenantMiddleware` already
  runs identically for `DeviceToken`-scheme requests, per feature 008a).
- **II. Regulatory Compliance by Design** — Pass, not applicable. This feature touches no BKR
  ratio, split-location, or closure-calendar logic.
- **III. CQRS via MediatR & Thin Endpoints** — Pass. The batch write is a new
  `RecordChildEventBatchCommand` handled by MediatR; `ChildEventEndpoints.cs` only maps HTTP to
  the command and maps the result back, per research.md R5's per-child loop living in the handler,
  not the endpoint.
- **IV. Internationalization First** — Pass. All new caregiver-facing strings (multi-select mode,
  batch result summary/retry) are added as locale keys across `en`/`nl`/`fr` (research.md R8); the
  batch API's error `reason` values are keys/codes, not display text, resolved client-side.
- **V. Test with Real Infrastructure** — Pass. Backend tests for the batch endpoint (happy path,
  partial failure, oversized batch, unsupported event type, R2's auth fix) run against
  TestContainers PostgreSQL, per the existing `ChildEvents`/`Attendance` test project conventions.
- **VI. Secure Configuration & Storage** — Pass, not applicable. No new secrets, storage, or
  migration.
- **VII. Monolith-First Simplicity** — Pass. No new project, no new service; the batch endpoint
  lives in the existing `ChildCare.Api`/`ChildCare.Application` projects alongside feature 009's
  code.

No violations; Complexity Tracking is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/009c-multi-child-events/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── child-events-batch-api.md
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Contracts/
│   ├── Requests/ChildEventRequests.cs          # + RecordChildEventBatchRequest
│   └── Responses/ChildEventResponses.cs        # + ChildEventBatchResponse
├── ChildCare.Application/ChildEvents/
│   ├── RecordChildEventBatchCommand.cs         # new — per-child loop (research.md R5),
│   │                                            #   presence check (R4), reuses existing
│   │                                            #   ChildEventPayloadValidator
│   └── ChildEventResult.cs                     # + ChildEventBatchFailureReason (data-model.md)
├── ChildCare.Api/
│   ├── Endpoints/ChildEventEndpoints.cs        # + POST /api/child-events/batch
│   ├── Endpoints/ChildrenEndpoints.cs          # R2 fix: reads group auth scheme extended
│   └── Endpoints/GroupsEndpoints.cs            # R2 fix: reads group auth scheme extended
│   └── Program.cs                              # R2 fix: new composite policy for the two
│                                                #   reads routes above (mirrors DeviceOrDirector)
└── ChildCare.Api.Tests/ChildEvents/
    └── RecordChildEventBatchTests.cs           # new

mobile/
├── app/(app)/index.tsx                         # multi-select mode on the room roster
├── components/QuickActionSheet.tsx             # batch mode: childIds[], filtered EVENT_TYPES
├── services/childEvents.ts                     # + recordChildEventBatch()
├── services/syncEngine.ts                      # R6: partial-failure handling for
│                                                #   entity_type = 'child_event_batch'
└── i18n/locales/{en,nl,fr}.json                # + groupView.multiSelect.*, childEvents.batch.*
```

**Structure Decision**: No new top-level directories. Backend additions live entirely inside the
existing `ChildEvents` vertical slice (`ChildCare.Application/ChildEvents/`,
`ChildCare.Api/Endpoints/ChildEventEndpoints.cs`), consistent with feature 009/009a. The R2 auth
fix touches `ChildrenEndpoints.cs`/`GroupsEndpoints.cs`/`Program.cs` directly rather than adding a
new file, since it's a one-line policy change per route, not new logic. Mobile changes are confined
to the existing room roster screen and `QuickActionSheet` — no new screens or navigation routes.

## Complexity Tracking

*No violations — table intentionally omitted.*
