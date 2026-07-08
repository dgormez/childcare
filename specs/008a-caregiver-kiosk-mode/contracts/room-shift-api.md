# Contract: Room Shift Register

All endpoints below except `PATCH /api/room-shifts/{id}`: auth = `DeviceToken` scheme only. No
user JWT, no per-caregiver HTTP credential — `staff_id`/`pin` are request-body content verified
server-side, never a bearer credential.

Every `DeviceToken`-authenticated call is additionally validated against the token's
`location_id` claim vs. the resource being acted on (FR-004): any call naming a `staff_id`
(check-in, check-out, confirm-administrator) MUST reject `403 errors.staff.not_eligible_here` if
that caregiver isn't eligible at the token's own location — token possession alone never grants
access to a caregiver outside its scope.

Select-then-PIN, not PIN-only (research.md R6): the caregiver identifies themselves by tapping
their own photo card before entering a PIN, so every endpoint here that verifies a PIN also
receives an explicit `staff_id` — the server verifies against that one record, it never searches
for whose PIN a submitted value might be.

## `GET /api/room-shifts/roster`

Powers the room home screen's photo-card grid (FR-013, research.md R7).

Response `200`: `[{ staffProfileId: Guid, firstName: string, photoUrl: string | null,
checkedIn: boolean, checkedInAt: string | null }]` — every caregiver eligible at this device's
location, ordered by `firstName`. `photoUrl: null` means the client renders a placeholder
avatar (FR-013) — never omit a caregiver for lack of a photo. Never empty as an array (a
location with zero eligible caregivers is a setup problem outside this feature's scope, not
represented specially here).

## `POST /api/room-shifts/check-in`

Request: `{ staffId: Guid, pin: string }`.

Response `200`: `{ staffProfileId: Guid, firstName: string, checkedInAt: string }`.

Response `409` `errors.room_shifts.already_checked_in` — this `staffId` already has an open
shift; the client's roster should already reflect this (stale-card protection, not the primary
flow — a caregiver only ever taps a not-checked-in card to reach this endpoint).

Response `403` `errors.staff.not_eligible_here` — `staffId` isn't eligible at this device's
location (FR-004), or the profile is deactivated (FR-024). Checked *before* the PIN comparison.

Response `401` `errors.pin.invalid` — incorrect PIN for this `staffId`, no state change.
Response includes `{ attemptsRemaining: int }` so the client can show a "2 attempts before
lockout" style hint.

Response `423` (Locked) `errors.pin.locked` with `{ lockedUntil: string }` — this caregiver's
PIN is in its 10-minute lockout window (research.md R2); no other caregiver's card is affected.

## `POST /api/room-shifts/check-out`

Request: `{ staffId: Guid, pin: string }` — same shape as check-in.

Response `200`: `{ staffProfileId: Guid, firstName: string, checkedOutAt: string }`.

Response `409` `errors.room_shifts.not_checked_in` — this `staffId` has no open shift to close.

Response `403`/`401`/`423`: identical semantics to check-in (FR-004/024, `errors.pin.invalid`,
`errors.pin.locked`) — the same shared per-caregiver lockout counter as check-in (spec
Clarifications, research.md R2).

## `POST /api/room-shifts/confirm-administrator`

For the sensitive-action confirmation step (FR-017/018) — deliberately generic, not tied to any
specific event type yet (research.md R4: no synthetic endpoint needed for the *routine*
write-action claim, but this confirmation step itself is real, reusable UI/API surface feature
009's medication/temperature events will call directly). Same select-then-PIN pattern as
check-in/out, narrowed on the client to only the currently-checked-in roster.

Request: `{ staffId: Guid, pin: string } | { skip: true }`.

Response `200` (`staffId`/`pin` provided, correct, **and** that caregiver currently has an open
`RoomShift`): `{ administeredByStaffProfileId: Guid }`. Response `200` (`skip: true`):
`{ administeredByStaffProfileId: null }` — always succeeds, per FR-018.

Response `409` `errors.room_shifts.not_checked_in` — `staffId` is valid and eligible, but has no
open shift right now (FR-017) — only a currently-present caregiver can be named administrator.
The app's own UI only ever offers checked-in caregivers to tap, so this should be unreachable
through normal use; the server enforces it independently regardless.

Response `401`/`423`: same `errors.pin.invalid`/`errors.pin.locked` shape as check-in, and
**shares the same per-caregiver lockout counter** (spec Clarifications, research.md R2) — a
failed attempt here counts toward the same `staffId`'s 5-in-2-minutes threshold as check-in/
check-out failures, not a separate counter.

Offline: this endpoint is not called at all when the device is offline — the mobile client
skips straight to `administeredByStaffProfileId: null` locally (spec US5 AC3) rather than
queuing a confirmation call for later replay, since confirming a specific individual after the
fact over a stale offline attempt doesn't make sense — a director completes it retroactively
instead.

## `PATCH /api/room-shifts/{id}`

Director-only correction of a shift's recorded times (FR-023) — covers both a forgotten
check-out (auto-closed at midnight, `ClosedReason: "auto_checkout"`) and any other recorded
mistake. Auth = user JWT (`DirectorOnly`), **not** `DeviceToken` — this is a web-admin-style
action a director performs from their own account, not something the tablet itself calls.

Request: `{ checkedInAt?: string, checkedOutAt?: string }` — at least one field.

Response `200`: the updated shift `{ id, staffProfileId, checkedInAt, checkedOutAt,
closedReason }`. The prior values, the new values, which director made the change, and when,
are recorded in a structured server-side log entry — the same audit rigor as FR-021's
revoked-device rejection logging (no separate audit table; see data-model.md).

Response `404` if the shift doesn't exist or isn't in the director's tenant.
