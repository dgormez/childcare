---
description: "Task list for feature implementation"
---

# Tasks: Caregiver App Kiosk Mode (Room Shift Register)

**Input**: Design documents from `/specs/008a-caregiver-kiosk-mode/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included. Constitution Principle V (NON-NEGOTIABLE) requires real infrastructure —
TestContainers-backed xUnit tests for every backend addition, and real (test-mode) SQLite for
the mobile device-auth/offline-queue integration, with only the network layer faked (matching
feature 008's own precedent). No synthetic test-only endpoint (research.md R4) — check-in/
check-out are the real proof of device-token-sufficiency, and `IShiftAttributionService` is
tested directly against seeded rows.

**Organization**: Tasks are grouped by user story (spec.md: US1–US4 P1, US5–US7 P2). The device-
token auth scheme, `room_shifts`/`device_pairings` schema, `StaffProfile` PIN columns, and
shared `VerifyPinCommand`/`IShiftAttributionService` are prerequisites every story depends on,
so they live in Foundational. Web-admin UI for PIN management/device revocation is explicitly
out of scope (spec Assumptions) — backend only, deferred to feature `007a-web-admin-scaffold`.

Select-then-PIN (BACKLOG.md revision, research.md R6): the caregiver taps their own photo card
before entering a PIN, so every check-in/check-out/confirm-administrator call carries an
explicit `staffId` — `VerifyPinCommand` looks that one `StaffProfile` up directly, it never
searches a candidate set.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US7) — omitted for Setup/Foundational/Polish
- File paths are exact and repository-root-relative

## Path Conventions

Backend (`backend/`, C#/.NET) and mobile (`mobile/`, TypeScript/Expo) — same two-project split
as feature 008.

---

## Phase 1: Setup

**Purpose**: Confirm baseline on both stacks before touching the auth pipeline.

- [X] T001 Confirm `dotnet build backend/ChildCare.sln` succeeds and `cd mobile && npm test` passes on this branch before starting

**Checkpoint**: Baseline confirmed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Device-token auth scheme, new schema, and the shared PIN-verification/shift-
attribution services every user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Backend: schema

- [X] T002 [P] Add `PinHash` (`string?`), `PinFailedAttempts` (`int`, default `0`), `PinFirstFailedAttemptAt` (`DateTime?`), `PinLockedUntil` (`DateTime?`) to `backend/ChildCare.Domain/Entities/StaffProfile.cs` (data-model.md) — a simple per-caregiver lockout counter, viable because select-then-PIN always gives `VerifyPinCommand` an explicit target (research.md R2)
- [X] T003 [P] Create `backend/ChildCare.Domain/Entities/RoomShift.cs` per data-model.md (`Id`, `StaffProfileId`, `LocationId`, `GroupId`, `DevicePairingId`, `CheckedInAt`, `CheckedOutAt`, `ClosedReason`, `CreatedAt`)
- [X] T004 [P] Create `backend/ChildCare.Domain/Entities/DevicePairing.cs` per data-model.md (`Id`, `TenantId`, `LocationId`, `GroupId`, `DirectorOverridePinHash`, `TokenIssuedAt`, `TokenVersion`, `RevokedAt`, `PairedByTenantUserId`, `OverridePinFailedAttempts`, `OverridePinFirstFailedAttemptAt`, `OverridePinLockedUntil`, `CreatedAt`) — the override-PIN lockout fields are a per-record counter, since the override PIN compares against exactly one target
- [X] T005 Register `RoomShift`/`DevicePairing` in `TenantDbContext` (`backend/ChildCare.Infrastructure/Persistence/TenantDbContext.cs`), generate the EF Core migration, and generate the SQL script per constitution VI (never auto-applied) — depends on T002, T003, T004

### Backend: device-token auth (research.md R1)

- [X] T006 [P] Add `backend/ChildCare.Api/Auth/DeviceTokenClaims.cs` — shared claim-name constants (`tenant_id`, `device_id`, `location_id`, `group_id`, `token_version`) reused by token issuance and validation
- [X] T007 Register a second named JWT bearer scheme (`"DeviceToken"`) in `backend/ChildCare.Api/Program.cs` with its own issuer/audience/signing key (distinct secret from the user-JWT key, sourced from configuration — constitution VI), and an `OnTokenValidated` event that rejects the request if `DevicePairing.RevokedAt` is set or `token_version` doesn't match `DevicePairing.TokenVersion` (revocation check happens on every request, not just issuance — FR-021). This authentication-layer check runs before any endpoint/command code (including `VerifyPinCommand`), which is what structurally guarantees FR-029's device-token-before-PIN precedence — depends on T004, T006
- [X] T008 Replace the default `"Bearer"` scheme registration in `Program.cs` with `AddPolicyScheme` that inspects the incoming token's `iss` claim (decode-without-validate) and forwards to either the real user-JWT scheme or `"DeviceToken"` — so `TenantMiddleware`'s existing `context.User.FindFirst("tenant_id")` code needs zero changes (research.md R1) — depends on T007
- [X] T009 Add a `"DeviceAuthenticated"` authorization policy (`RequireAuthenticationSchemes("DeviceToken")` + `RequireAuthenticatedUser()`) in `Program.cs`, alongside the existing `DirectorOnly`/`StaffOrDirector`/`ParentOnly` policies — depends on T007

### Backend: shared services

- [X] T010 [P] Create `backend/ChildCare.Application/Staff/VerifyPinCommand.cs` — takes `(locationId, staffId, pin)`, not just `(locationId, pin)` (research.md R2/R6, select-then-PIN). Loads exactly the one `StaffProfile` by `staffId`; rejects `403 errors.staff.not_eligible_here` outright — *before* touching the PIN — if it's deactivated or not eligible at `locationId` (`StaffLocationEligibility`; this single check is what makes FR-004/024/025 fall out of one code path). Otherwise bcrypt-compares the PIN against that profile's `PinHash`: on failure, increments its `PinFailedAttempts`/`PinFirstFailedAttemptAt` sliding-window streak (anchored to the first failure, not a fixed clock window — data-model.md) and sets `PinLockedUntil` at 5 failures within 2 minutes; on success, resets the streak. No candidate-set search — used by check-in, check-out, and sensitive-action confirmation alike — depends on T002
- [X] T011 [P] Create `backend/ChildCare.Application/RoomShifts/IShiftAttributionService.cs` + implementation — `ResolveRecordedByAsync(locationId, groupId, occurredAtUtc)` returning every `StaffProfileId` with an open `RoomShift` covering that instant (data-model.md) — depends on T003
- [X] T012 [P] Create `backend/ChildCare.Application/RoomShifts/CloseStaleShiftsHelper.cs` — closes any `RoomShift` still open from a previous calendar day (local midnight boundary), setting `CheckedOutAt`/`ClosedReason = "auto_checkout"` (research.md R5, lazy materialization, no scheduled job) — depends on T003
- [X] T013 [P] Add `errors.pin.invalid`, `errors.pin.locked`, `errors.staff.not_eligible_here`, `errors.room_shifts.already_checked_in`, `errors.room_shifts.not_checked_in`, `errors.devices.already_paired_this_session`, `errors.devices.invalid_override_pin`, `errors.devices.override_pin_locked`, `errors.devices.revoked`, `errors.devices.token_expired`, `errors.pin.not_unique_at_location` to `backend/ERROR_KEYS.md`

### Mobile: shared plumbing

- [X] T014 [P] Create `mobile/services/deviceAuth.ts` scaffold — device-token storage (`SecureStore`, distinct key from feature 008's user-session refresh token), `getDeviceToken()`/`storeDeviceToken()`/`clearDeviceCredentials()` — depends on nothing new (reuses feature 008's `expo-secure-store` setup)
- [X] T015 Extend `mobile/services/apiClient.ts`'s auth middleware: once a device token is stored, attach it (not the user-session token) to outgoing requests, and read an `X-Device-Token-Refresh` response header to swap the stored token immediately (research.md R3) — depends on T014
- [X] T016 [P] Add i18n keys for this feature (`roomSetup.*`, `roomHome.*`, `pin.*`, `devices.*`) to `mobile/i18n/locales/{en,nl,fr}.json`

**Checkpoint**: Auth pipeline, schema, and shared services exist. No user-facing behavior yet.

---

## Phase 3: User Story 1 - Director Pairs a Tablet (Priority: P1) 🎯 MVP

**Goal**: A director can pair a fresh tablet to a location/group and it locks into room mode.

**Independent Test**: Sign in as director on a fresh install, complete room setup, force-quit
and reopen — confirm it reopens directly into room mode with no re-authentication.

### Tests for User Story 1

- [ ] T017 [P] [US1] Backend integration test: `POST /api/devices/pair` as a director issues a device token scoped to the chosen location/group, and the returned token is immediately usable against a `DeviceAuthenticated` endpoint — in `backend/ChildCare.Api.Tests/DevicePairingTests.cs` — depends on T007, T008, T009
- [ ] T018 [P] [US1] Backend integration test: `POST /api/devices/exit-room-mode` with the correct director-override PIN succeeds; with an incorrect one, fails without touching caregiver-PIN lockout (contracts/device-pairing-api.md — the override PIN compares against exactly one target, `DevicePairing.DirectorOverridePinHash`, so it uses its own `OverridePinFailedAttempts`/`OverridePinLockedUntil` fields); 10 incorrect attempts trigger `423 errors.devices.override_pin_locked` for 30 minutes on that device specifically (spec FR-005) — same file
- [ ] T019 [P] [US1] Mobile test: room-setup flow calls the pairing endpoint, stores the returned device token, and the app shell treats "device token present" as "enter room mode" on next launch — in `mobile/__tests__/services/deviceAuth.test.ts`
- [ ] T020 [P] [US1] Backend integration test: exiting room mode (correct override PIN) auto-closes any `RoomShift` still open under that tablet's `DevicePairingId` (`ClosedReason = "reassigned"`) — proves FR-026 — same file — depends on T003, T004

### Implementation for User Story 1

- [X] T021 [US1] Create `backend/ChildCare.Application/Devices/PairDeviceCommand.cs` + FluentValidation — issues a `DevicePairing` row and a signed device token (`token_version = 1`) — depends on T004, T006
- [X] T022 [US1] Create `backend/ChildCare.Application/Devices/ExitRoomModeCommand.cs` — verifies the director-override PIN via bcrypt against `DevicePairing.DirectorOverridePinHash`, tracking its own `OverridePinFailedAttempts`/`OverridePinFirstFailedAttemptAt`/`OverridePinLockedUntil` sliding-window lockout directly on that row (a single-target comparison, so a per-record counter is unambiguous — unlike `VerifyPinCommand`, contracts/device-pairing-api.md's explicit exception). On success, also closes any `RoomShift` still open under this `DevicePairingId` (`ClosedReason = "reassigned"`, FR-026) — depends on T003, T004
- [X] T023 [US1] Create `backend/ChildCare.Api/Endpoints/DevicePairingEndpoints.cs`: `POST /api/devices/pair` (`DirectorOnly`), `POST /api/devices/exit-room-mode` (`DeviceAuthenticated`) — depends on T021, T022
- [X] T024 [US1] Create `mobile/app/(room-setup)/index.tsx`: location/group picker + director-override-PIN entry, calling feature 008's existing email/password login first, then the pairing endpoint — depends on T014, T015
- [X] T025 [US1] Update `mobile/app/_layout.tsx`: on launch, check for a stored device token before falling back to feature 008's user-session check — device token present routes to `(room)`, absent routes to `(room-setup)`/`(auth)/login` as today — depends on T014, T024
- [X] T026 [US1] Add the director-override-PIN exit action (calls `POST /api/devices/exit-room-mode`) somewhere reachable but not accidental in the room shell — depends on T024

**Checkpoint**: A tablet can be paired and re-paired; room mode persists across restarts; reassignment closes prior shifts.

---

## Phase 4: User Story 2 - Director Manages Caregiver PINs (Priority: P1)

**Goal**: A director can set/reset a caregiver's PIN via the API (no UI in this feature — spec Assumptions).

**Independent Test**: Set a PIN, confirm it's hashed and usable; attempt a duplicate at the same location, confirm rejection.

### Tests for User Story 2

- [ ] T027 [P] [US2] Backend integration test: `PUT /api/staff/{id}/pin` stores a bcrypt hash, never the plaintext PIN, and the caregiver's account password is unaffected — in `backend/ChildCare.Api.Tests/PinManagementTests.cs` — depends on T002
- [ ] T028 [P] [US2] Backend integration test: setting the same PIN for a second caregiver eligible at the same location is rejected `409 errors.pin.not_unique_at_location`; the same PIN at a *different*, non-overlapping location succeeds — same file
- [ ] T029 [P] [US2] Backend integration test: `DELETE /api/staff/{id}/pin` clears the hash; the caregiver can no longer check in until a new PIN is set — same file

### Implementation for User Story 2

- [X] T030 [US2] Create `backend/ChildCare.Application/Staff/SetCaregiverPinCommand.cs` + FluentValidation (4 numeric digits, uniqueness check against `StaffLocationEligibility`-derived candidate set per data-model.md, resets `PinFailedAttempts`/`PinFirstFailedAttemptAt`/`PinLockedUntil` to defaults) — depends on T002
- [X] T031 [US2] Create `backend/ChildCare.Application/Staff/DeleteCaregiverPinCommand.cs` — depends on T002
- [X] T032 [US2] Add `PUT /api/staff/{id}/pin` and `DELETE /api/staff/{id}/pin` (`DirectorOnly`) to `backend/ChildCare.Api/Endpoints/StaffEndpoints.cs` — depends on T030, T031

**Checkpoint**: PINs can be provisioned — User Story 3 is now testable end-to-end.

---

## Phase 5: User Story 3 - Caregiver Checks In and Out via PIN (Priority: P1)

**Goal**: Multiple caregivers can be simultaneously checked in to the same room via select-then-PIN.

**Independent Test**: Tap caregiver A's card and check in, tap caregiver B's (different) card and
check in without checking A out, confirm both cards show checked in, then check each out
independently the same way.

### Tests for User Story 3

- [ ] T033 [P] [US3] Backend integration test: `GET /api/room-shifts/roster` returns every caregiver eligible at this device's location as a card (`staffProfileId`, `firstName`, `photoUrl`, `checkedIn: false`), including one with no profile photo (`photoUrl: null`, never omitted) — in `backend/ChildCare.Api.Tests/RoomShiftTests.cs` — depends on T003, T004
- [ ] T034 [P] [US3] Backend integration test: `POST /api/room-shifts/check-in` with `{ staffId, pin }` for a not-yet-checked-in caregiver records a check-in, and the roster reflects `checkedIn: true` immediately after — same file — depends on T010
- [ ] T035 [P] [US3] Backend integration test: `POST /api/room-shifts/check-out` with `{ staffId, pin }` for a checked-in caregiver records a check-out and removes them from the checked-in set — same file — depends on T010
- [ ] T036 [P] [US3] Backend integration test: caregiver A and caregiver B (different `staffId`/PIN) can be checked in simultaneously — both show `checkedIn: true` on the roster, neither check-in blocks the other — same file — depends on T010
- [ ] T037 [P] [US3] Backend integration test: 5 incorrect PIN attempts for caregiver A within 2 minutes locks A's check-in/check-out/confirmation ability for 10 minutes; caregiver B's card is entirely unaffected (spec FR-012); a request arriving *during* A's active lockout returns the identical locked-response shape as the triggering request (CHK008); 4 failures for A spaced just past the sliding window each time never triggers a lockout, proving the streak is anchored to the first failure and not a fixed clock-aligned window (CHK007) — same file — depends on T010
- [ ] T038 [P] [US3] Backend integration test: `POST /api/room-shifts/check-in` for a `staffId` that already has an open shift returns `409 errors.room_shifts.already_checked_in` — same file — depends on T010
- [ ] T039 [P] [US3] Backend integration test: `POST /api/room-shifts/check-out` for a `staffId` with no open shift returns `409 errors.room_shifts.not_checked_in` — same file — depends on T010
- [ ] T040 [P] [US3] Mobile test: room home renders the roster as photo cards; tapping a not-checked-in card opens a PIN keypad overlay addressed by name; submitting a correct PIN closes the overlay and shows the card as checked in — in `mobile/__tests__/screens/room-home.test.tsx`
- [ ] T041 [P] [US3] Mobile test: two cards can both show the checked-in state simultaneously, updating immediately after each check-in/out — same file
- [ ] T042 [P] [US3] Backend integration test: `PATCH /api/room-shifts/{id}` as a director updates `CheckedInAt`/`CheckedOutAt` on a shift and emits a structured audit log entry (who corrected, when, prior values) with the same rigor as FR-021's revoked-device audit logging (spec FR-023, CHK006) — in `backend/ChildCare.Api.Tests/RoomShiftTests.cs` — depends on T003
- [ ] T043 [P] [US3] Backend integration test: deactivating a caregiver while checked in closes their open `RoomShift` immediately (`ClosedReason = "deactivated"`), and their `staffId` is rejected `403 errors.staff.not_eligible_here` on a subsequent check-in attempt regardless of PIN correctness (FR-024) — same file — depends on T003, T010
- [ ] T044 [P] [US3] Backend integration test: a caregiver eligible at two locations can check in with the same PIN via either location's tablet (distinct `staffId` lookup, same person); removing their eligibility at location A blocks check-in there on the *next* attempt while location B remains usable — proves eligibility is evaluated fresh per call, not cached (FR-025) — same file — depends on T010

### Implementation for User Story 3

- [X] T045 [US3] Create `backend/ChildCare.Application/RoomShifts/CheckInCommand.cs` — calls `VerifyPinCommand`, then `CloseStaleShiftsHelper`, then rejects `409 errors.room_shifts.already_checked_in` if an open shift already exists for `staffId`, otherwise creates one — depends on T010, T012
- [X] T046 [US3] Create `backend/ChildCare.Application/RoomShifts/CheckOutCommand.cs` — calls `VerifyPinCommand`, then `CloseStaleShiftsHelper`, then rejects `409 errors.room_shifts.not_checked_in` if no open shift exists for `staffId`, otherwise closes it — depends on T010, T012
- [X] T047 [US3] Create `backend/ChildCare.Application/RoomShifts/GetRoomRosterQuery.cs` (research.md R7) — closes stale shifts first (`CloseStaleShiftsHelper`), then returns every `StaffProfile` eligible at the device's `LocationId`, joined with `IProfilePhotoStorage`'s photo URL (feature 005, placeholder if unset) and current `RoomShift` state — depends on T003, T012
- [X] T048 [US3] Create `backend/ChildCare.Api/Endpoints/RoomShiftEndpoints.cs`: `GET /api/room-shifts/roster`, `POST /api/room-shifts/check-in`, `POST /api/room-shifts/check-out` (all `DeviceAuthenticated`) — depends on T045, T046, T047, T009
- [X] T049 [US3] Create `mobile/services/roomShift.ts`: `getRoster()`, `checkIn(staffId, pin)`, `checkOut(staffId, pin)` — depends on T015
- [X] T050 [US3] Create `mobile/app/(room)/_layout.tsx`: the kiosk shell (replaces `(app)/_layout.tsx` as the daily entry point for a paired tablet) — reuses feature 008's offline banner pattern — depends on T025
- [X] T051 [US3] Create `mobile/app/(room)/index.tsx`: room home screen — location/group/date header, roster as photo cards (checked-in cards visually distinct), tap-to-open PIN keypad overlay addressed by name (64pt targets — FR-028) — depends on T049, T050
- [ ] T052 [US3] Wire check-in/out through feature 008's offline queue (`entity_type = 'room_shift'`) so it queues when offline and replays on reconnect — depends on T049
- [X] T053 [P] [US3] Create `backend/ChildCare.Application/RoomShifts/CorrectShiftCommand.cs` — director-only update to a `RoomShift`'s `CheckedInAt`/`CheckedOutAt`, and emits a structured audit log entry (staff, correcting tenant user, prior values, new values, timestamp) via `ILogger`, matching FR-021's server-side audit-logging approach — no new audit table (data-model.md) — depends on T003
- [X] T054 [US3] Add `PATCH /api/room-shifts/{id}` (`DirectorOnly`) to `RoomShiftEndpoints.cs` — depends on T053, T009
- [X] T055 [US3] Extend `DeactivateStaffProfileCommandHandler` (`backend/ChildCare.Application/Staff/DeactivateStaffProfileCommand.cs`, feature 005) to close any open `RoomShift` for the deactivated `StaffProfileId` (`ClosedReason = "deactivated"`) — depends on T003

**Checkpoint**: Core shift-register loop works end-to-end via photo-card select-then-PIN, including the two-simultaneous-caregivers case, director corrections, deactivation, and dual-location eligibility.

---

## Phase 6: User Story 4 - Authenticated Actions Need No Individual Login (Priority: P1)

**Goal**: Prove the device token alone is sufficient auth, and that attribution resolves correctly.

**Independent Test**: Submit a device-authenticated write with zero/one/two caregivers checked in; confirm it never blocks and attribution matches each case.

### Tests for User Story 4

- [ ] T056 [P] [US4] Backend integration test: `IShiftAttributionService.ResolveRecordedByAsync` returns `[]` when nobody is checked in, `[A]` when exactly A is, `[A, B]` when both are — in `backend/ChildCare.Api.Tests/ShiftAttributionServiceTests.cs` — depends on T011
- [ ] T057 [P] [US4] Backend integration test: `POST /api/room-shifts/check-in` (proof of "device-token-only, no individual HTTP-auth gate") succeeds with `{ staffId, pin }` and zero *other* caregivers checked in — reuses `RoomShiftTests.cs` — depends on T045
- [ ] T058 [P] [US4] Backend integration test: any `DeviceAuthenticated` endpoint rejects a request with no device token, an expired one, or a revoked one, regardless of shift-log state — same file — depends on T007
- [ ] T059 [P] [US4] Backend integration test: a `staffId` belonging to a caregiver eligible only at a *different* location than the device token's own is rejected `403 errors.staff.not_eligible_here` on check-in, regardless of whether the PIN is correct — proves FR-004's token-scope-vs-resource validation (CHK001) — in `backend/ChildCare.Api.Tests/DevicePairingTests.cs` — depends on T010
- [ ] T060 [P] [US4] Mobile test: after pairing, `apiClient` attaches the device token (not a user-session token) to outgoing requests, with no separate HTTP-auth prompt anywhere in that path — in `mobile/__tests__/services/apiClient.test.ts` (extends feature 008's file) — depends on T015

### Implementation for User Story 4

*No new production code — this story is entirely a verification/proof pass over Foundational
(T007–T010) and User Story 3's real endpoints (research.md R4). If any test above fails, fix
the underlying Foundational/US3 code, don't add scaffolding.*

**Checkpoint**: The core auth-composition claim (device token = sufficient, PIN = presence not
auth) is proven end-to-end, not just asserted in the spec.

---

## Phase 7: User Story 5 - Medical-Action Administrator Confirmation (Priority: P2)

**Goal**: A reusable select-then-PIN confirmation step for sensitive actions, shared lockout with check-in/out.

**Independent Test**: Tap a checked-in caregiver's card and confirm with a correct PIN →
administrator recorded; confirm with skip → action still completes, field unset.

### Tests for User Story 5

- [ ] T061 [P] [US5] Backend integration test: `POST /api/room-shifts/confirm-administrator` with `{ staffId, pin }` for a currently-checked-in caregiver returns `administeredByStaffProfileId` set to that caregiver — in `backend/ChildCare.Api.Tests/RoomShiftTests.cs` — depends on T010
- [ ] T062 [P] [US5] Backend integration test: `POST /api/room-shifts/confirm-administrator` with a `staffId` that is valid/eligible but *not* currently checked in returns `409 errors.room_shifts.not_checked_in`, regardless of whether the PIN is correct (spec FR-017, CHK002) — same file — depends on T010
- [ ] T063 [P] [US5] Backend integration test: `{ skip: true }` always succeeds with `administeredByStaffProfileId: null` — same file
- [ ] T064 [P] [US5] Backend integration test: a failed attempt on this endpoint counts toward the *same `staffId`'s* shared lockout counter as check-in/check-out (spec Clarifications, research.md R2) — proves T010's shared-counter design, not a separate one — same file
- [ ] T065 [P] [US5] Mobile test: the confirmation component shows only the currently-checked-in roster as cards, calls confirm-administrator with the tapped card's `staffId` when online, and skips straight to `null` locally when offline without queuing a call (spec US5 AC3) — in `mobile/__tests__/components/AdministratorConfirmation.test.tsx`

### Implementation for User Story 5

- [ ] T066 [US5] Create `backend/ChildCare.Application/RoomShifts/ConfirmAdministratorCommand.cs` — calls the same `VerifyPinCommand` as check-in/out, then rejects `409 errors.room_shifts.not_checked_in` if the verified caregiver has no currently-open `RoomShift` (FR-017 — a valid PIN alone is insufficient, the caregiver must actually be checked in) — depends on T010, T003
- [ ] T067 [US5] Add `POST /api/room-shifts/confirm-administrator` (`DeviceAuthenticated`) to `RoomShiftEndpoints.cs` — depends on T066
- [ ] T068 [US5] Create `mobile/components/AdministratorConfirmation.tsx` — reusable card-roster (checked-in caregivers only, from `getRoster()`) + PIN-keypad-overlay sheet with a Skip action, checking `useNetworkStatus()` to skip the API call entirely when offline — depends on T049, `useNetworkStatus` (feature 008)

**Checkpoint**: Sensitive-action confirmation exists as reusable surface for feature 009's medical events.

---

## Phase 8: User Story 6 - Device Token Rotates Silently (Priority: P2)

**Goal**: Tokens nearing expiry rotate transparently without breaking offline-replay bursts.

**Independent Test**: Force a near-expiry token, make a request, confirm rotation header + old-token invalidation; confirm a burst of pre-rotation-token requests still succeeds.

### Tests for User Story 6

- [ ] T069 [P] [US6] Backend integration test: a device token with < 7 days remaining triggers an `X-Device-Token-Refresh` response header on the *next* authenticated request, and `DevicePairing.TokenVersion` increments — in `backend/ChildCare.Api.Tests/DeviceTokenRotationTests.cs` — depends on T007
- [ ] T070 [P] [US6] Backend integration test: a batch of requests already carrying the pre-rotation token (simulating an offline-queue replay burst) all still succeed — same file
- [ ] T071 [P] [US6] Backend integration test: a fully expired (30-day) token is rejected `401 device.token_expired`, distinguishable from `device.revoked` — same file
- [ ] T072 [P] [US6] Mobile test: `apiClient` reads `X-Device-Token-Refresh` and swaps the stored token before the *next* call, without interrupting the response already in flight — extends `mobile/__tests__/services/apiClient.test.ts` — depends on T015

### Implementation for User Story 6

- [ ] T073 [US6] Create an `IEndpointFilter` (`backend/ChildCare.Api/Auth/DeviceTokenRotationFilter.cs`) applied to the `DeviceAuthenticated`-authorized route group: after the response is produced, if the validated token has < 7 days remaining, mint a replacement (increment `TokenVersion`, new `TokenIssuedAt`) and add `X-Device-Token-Refresh` (research.md R3). This filter only ever runs on a request that already passed T007's `OnTokenValidated` revocation check, which structurally guarantees FR-030 (a revoked device never gets a rotated replacement) — depends on T007, T009
- [ ] T074 [US6] Add the `device.token_expired` vs `device.revoked` distinction to the `OnTokenValidated`/challenge handling from T007 — depends on T007

**Checkpoint**: Long-lived tablets stay authenticated indefinitely without caregiver-visible interruption.

---

## Phase 9: User Story 7 - Director Revokes a Lost or Stolen Tablet (Priority: P2)

**Goal**: A revoked tablet is locked out immediately, not at passive expiry.

**Independent Test**: Revoke a paired tablet, confirm its very next request is rejected; confirm offline-queued actions from it are rejected on sync and logged.

### Tests for User Story 7

- [ ] T075 [P] [US7] Backend integration test: `POST /api/devices/{id}/revoke` as a director causes the *very next* request from that device's token to fail `401 device.revoked`, independent of remaining TTL — in `backend/ChildCare.Api.Tests/DevicePairingTests.cs` — depends on T007
- [ ] T076 [P] [US7] Backend integration test: an offline-queued action from a since-revoked device is rejected on sync and the rejection is logged server-side (audit) — same file
- [ ] T077 [P] [US7] Mobile test: receiving a `device.revoked` response clears all `SecureStore` credentials and cached tenant data and routes back to `(room-setup)` — in `mobile/__tests__/services/deviceAuth.test.ts`

### Implementation for User Story 7

- [X] T078 [US7] Create `backend/ChildCare.Application/Devices/RevokeDeviceCommand.cs` — sets `DevicePairing.RevokedAt` — depends on T004
- [X] T079 [US7] Add `POST /api/devices/{id}/revoke` (`DirectorOnly`) to `DevicePairingEndpoints.cs` — depends on T078
- [ ] T080 [US7] Handle `device.revoked`/`device.token_expired` in `mobile/services/deviceAuth.ts`: clear `SecureStore` (device token + user-session leftovers) and feature 008's local SQLite tenant data, redirect to `(room-setup)` — depends on T014, T025

**Checkpoint**: All seven user stories independently functional.

---

## Phase 10: Polish & Cross-Cutting Concerns

- [ ] T081 [P] Run `dotnet test backend/ChildCare.sln` and confirm no regressions in features 001–008
- [ ] T082 [P] Run `cd mobile && npm test` and confirm the full suite passes, including new device-auth/room-shift tests
- [ ] T083 Manually walk through every scenario in `quickstart.md`, timing Scenario 3's tap-card-then-PIN check-in/check-out against SC-001's 5-second target (or confirm via automated tests already covering the rest)
- [ ] T084 Confirm every new mobile string has NL/FR/EN i18n keys (constitution Principle IV) — no hardcoded strings in `(room-setup)`/`(room)` screens
- [ ] T085 Design compliance review (static, per `design-system.md`/`platform-rules.md`) on the new room-setup/room-home photo-card grid/PIN-keypad-overlay screens — 64pt PIN targets, photo-card sizing/placeholder-avatar treatment, 8/12px radius scale, v2 color tokens, no raw Tailwind colors

---

## Dependencies & Execution Order

- Phase 1 → Phase 2 (Foundational) → Phase 3 (US1) → Phase 4 (US2) → Phase 5 (US3) → Phase 6 (US4, verification-only) → Phase 7 (US5) → Phase 8 (US6) → Phase 9 (US7) → Phase 10 (Polish).
- US1–US4 are P1 and sequential in practice (US3 needs US2's PINs to exist; US4 needs US3's real endpoints to verify against) — treat as the MVP slice.
- US5–US7 (P2) can be built in any order relative to each other once US1–US4 are done; all three build on Foundational + US3's real endpoints, not on each other.

## Implementation Strategy

**MVP** = Phases 1–6 (Setup, Foundational, US1–US4): a tablet can be paired, PINs provisioned,
caregivers check in/out via select-then-PIN (including simultaneously), and the
device-token-sufficiency claim is proven. US5–US7 (medical-action confirmation, silent
rotation, revocation) are real security/UX requirements but not needed to demonstrate the core
shift-register model end-to-end.
