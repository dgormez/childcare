# Implementation Plan: Caregiver App Kiosk Mode (Room Shift Register)

**Branch**: `008a-caregiver-kiosk-mode` | **Date**: 2026-07-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/008a-caregiver-kiosk-mode/spec.md`

## Summary

Replace feature 008's personal-login model with a two-layer auth scheme: a long-lived,
revocable **device token** identifying a paired room tablet (the actual security boundary —
every API call needs one), and a server-side **shift register** tracking which caregiver(s) are
physically checked in via PIN, which is presence/accountability tracking, not a second HTTP
auth layer. Backend adds a second JWT bearer scheme (device tokens), a `room_shifts` table, a
`pin_hash` column on `StaffProfile`, and a reusable `IShiftAttributionService` that any future
caregiver-write feature (009, 010) calls to resolve `recorded_by`. Mobile replaces feature 008's
login screen with a room-setup flow, a PIN-keypad home screen, and swaps the stored credential
the API client attaches from a user session token to the device token once paired.

## Technical Context

**Language/Version**: C# / .NET 10 (backend); TypeScript 5 / Expo SDK (mobile) — same stack as
feature 008, no new language/runtime.

**Primary Dependencies**: ASP.NET Core Minimal APIs, MediatR, FluentValidation, EF Core 9,
`Microsoft.AspNetCore.Authentication.JwtBearer` (a second named scheme, not a new package) on
the backend. `expo-secure-store`, `openapi-fetch`, Zustand, `expo-router` on mobile — all
already in place from feature 008; no new mobile dependencies.

**Storage**: PostgreSQL 16, tenant schema (existing multi-tenant architecture) — new
`room_shifts` table, new `device_pairings` table, new `pin_hash`/`pin_failed_attempts`/
`pin_locked_until` columns on the existing `staff_profiles` table (feature 005).

**Testing**: xUnit + TestContainers-backed PostgreSQL (backend, constitution Principle V) — real
integration tests for device-token issuance/validation/rotation/revocation, check-in/check-out,
PIN lockout, and `IShiftAttributionService`. Jest + jest-expo (mobile) — real (test-mode) SQLite
for the room-mode/offline-queue integration, mirroring feature 008.

**Target Platform**: Backend: ASP.NET Core API on Cloud Run (unchanged). Mobile: caregiver
tablet, landscape, kiosk-locked (unchanged surface from feature 008, different daily entry
point).

**Project Type**: Mixed (backend + mobile) — same two projects feature 008 touched.

**Performance Goals**: Device-token validation (signature check + revocation-list lookup) MUST
stay cheap enough not to add material latency to offline-sync replay bursts (a caregiver's
tablet reconnecting after hours offline may replay dozens of queued requests in quick
succession, each needing this check).

**Constraints**: Offline-capable (check-in/out queues through feature 008's existing
`offline_queue`, `entity_type = 'room_shift'`); PIN never logged or stored in recoverable form;
PIN lockout state is per-PIN, shared across every surface that validates it (check-in,
check-out, sensitive-action confirmation — spec Clarifications); device token rotation must
never break an in-flight offline-queue replay.

**Scale/Scope**: A handful of paired tablets per organisation, a few caregivers checked in per
room at once — this is not a high-volume path; correctness and security matter far more than
throughput here.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|---|---|---|
| I. Multi-Tenant Isolation | PASS | `room_shifts`/`device_pairings` are tenant-schema tables. The device token carries `tenant_id` like the user JWT does; `TenantMiddleware` needs zero code changes — see research.md R1 for how both credential types populate `HttpContext.User` identically. |
| II. Regulatory Compliance by Design | PASS (scope note) | BKR ratio *enforcement* (blocking a check-in that would exceed the ratio) is explicitly not part of this feature's scope — the spec only tracks presence. Documented as an Assumption; ratio enforcement, if ever needed at check-in time, is a future feature's job. |
| III. CQRS via MediatR & Thin Endpoints | PASS | All writes (pairing, PIN set/reset, check-in/out, revoke) go through MediatR commands with FluentValidation; `IShiftAttributionService` is a plain injectable domain service, not a command, since it's a read-time resolution step other features' handlers call. |
| IV. Internationalization First | PASS | All new mobile strings (room setup, PIN keypad, lockout/error states, reactivation-required screen) get NL/FR/EN i18n keys from the start. |
| V. Test with Real Infrastructure | PASS | TestContainers-backed integration tests for the full device-token lifecycle and shift register; no synthetic test-only endpoint needed — check-in/check-out are real endpoints that directly prove "device token is sufficient auth" (see research.md R4), and `IShiftAttributionService` is tested directly against seeded `room_shifts` rows. |
| VI. Secure Configuration & Storage | PASS | Device-token signing key is a distinct secret from the user-JWT signing key, sourced from configuration/secrets manager, never hardcoded. PINs are bcrypt-hashed. No EF migration auto-applies in production — SQL script generated and reviewed. |
| VII. Monolith-First Simplicity | PASS | No new backend project, no new service — everything lives in the existing five projects. |

**Constitution amendment made during this planning pass**: the "Auth" stack-constraint bullet
("Caregiver app: email/password only") was stale against this feature's whole premise. Amended
to describe the room-tablet model explicitly (v1.1.0 → v1.2.0) rather than bending the plan
around an outdated constraint — same category of fix as the v1.1.0 amendment for feature 001.

**Post-Phase-1 re-check**: research.md's five decisions (policy-scheme auth forwarding,
app-level PIN lockout, endpoint-filter rotation, no synthetic test endpoint, lazy
auto-checkout) introduce no new services, no new tenant-isolation surface, and no deviation
from MediatR/FluentValidation for writes — the table above still holds after design.

## Project Structure

### Documentation (this feature)

```text
specs/008a-caregiver-kiosk-mode/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit-tasks, not this command)
```

### Source Code (repository root)

```text
backend/
├── ChildCare.Api/
│   ├── Auth/
│   │   ├── DeviceTokenClaims.cs            # shared claim-name constants (tenant_id/location_id/group_id/device_id)
│   │   └── DeviceTokenRotationFilter.cs    # IEndpointFilter, not a command (research.md R3)
│   ├── Endpoints/
│   │   ├── DevicePairingEndpoints.cs       # POST /api/devices/pair, /exit-room-mode, /{id}/revoke
│   │   └── RoomShiftEndpoints.cs           # GET /roster, POST /check-in, /check-out,
│   │                                       # /confirm-administrator, PATCH /{id} (director correction)
│   └── Program.cs                          # extended: second JWT bearer scheme + policy-scheme forwarder (research.md R1)
├── ChildCare.Application/
│   ├── Devices/
│   │   ├── PairDeviceCommand.cs
│   │   ├── ExitRoomModeCommand.cs           # also closes prior open shifts on reassignment (FR-026)
│   │   └── RevokeDeviceCommand.cs
│   ├── Staff/
│   │   ├── SetCaregiverPinCommand.cs        # extends feature 005's staff management
│   │   ├── DeleteCaregiverPinCommand.cs
│   │   └── VerifyPinCommand.cs              # shared PIN-check: takes (locationId, staffId, pin) directly —
│   │                                        # select-then-PIN means no candidate-set search (research.md R2/R6)
│   └── RoomShifts/
│       ├── CheckInCommand.cs
│       ├── CheckOutCommand.cs
│       ├── ConfirmAdministratorCommand.cs   # synthetic-action-agnostic confirmation, select-then-PIN (FR-017)
│       ├── CorrectShiftCommand.cs           # director-only correction + audit log (FR-023)
│       ├── GetRoomRosterQuery.cs            # every location-eligible caregiver + photo + checked-in state (research.md R7)
│       └── IShiftAttributionService.cs      # reusable recorded_by/administered_by resolver for 009/010
├── ChildCare.Domain/Entities/
│   ├── RoomShift.cs
│   └── DevicePairing.cs
└── ChildCare.Infrastructure/Persistence/Migrations/
    └── <generated>_AddRoomShiftsAndDevicePairings.cs

mobile/
├── app/
│   ├── (room-setup)/
│   │   └── index.tsx                       # director pairing flow — replaces reachability of (auth)/login.tsx for caregivers
│   └── (room)/
│       ├── _layout.tsx                     # kiosk shell — replaces (app)/_layout.tsx as the daily entry point
│       └── index.tsx                       # room home screen: photo-card roster + PIN keypad overlay (select-then-PIN)
├── services/
│   ├── deviceAuth.ts                       # pairing, device-token storage/rotation, revocation handling
│   └── roomShift.ts                        # roster, check-in/check-out, PIN confirmation calls
└── theme/, hooks/, components/              # reused as-is from feature 008 (v2 palette, ThemedModal, etc.)
```

**Structure Decision**: Same two-project split as feature 008 (backend + mobile). No new
top-level projects. The mobile app gains two new route groups — `(room-setup)` (director-only,
one-time) and `(room)` (the new daily kiosk shell) — while feature 008's `(auth)/login.tsx`
remains reachable only via the director-override PIN exit path, not as the caregiver's daily
entry point.

## Complexity Tracking

*No unjustified violations — Constitution Check is a clean pass after the amendment above.*
