# Research: Configurable Caregiver PIN

## R1 — Where the setting lives and how it's enforced

**Decision**: Add `Location.RequiresCaregiverPin` (bool, default `true`) as a plain auto-property,
same convention as `FlexPermission`/`BoPermission` (feature 004). Enforcement happens entirely
server-side in `CheckInCommandHandler`/`CheckOutCommandHandler`: before calling
`verifyPin.VerifyAsync(...)`, load the location and branch — if `RequiresCaregiverPin` is
`false`, skip the call and write the `RoomShift` row directly off `request.StaffId`; if `true`,
existing behavior (call `VerifyAsync`, reject on failure) is unchanged.

**Rationale**: Matches FR-007 — the server, not the tablet, is the enforcement point, so a
client cannot bypass PIN verification by simply omitting a flag. Mirrors the existing
`ConfirmAdministratorCommand.Skip` precedent (a server-side branch before the shared
`VerifyPinCommand.VerifyAsync` call), which is already the established pattern for
"conditionally skip PIN verification" in this codebase.

**Alternatives considered**: A client-supplied "skip PIN" flag on `CheckInRequest`/
`CheckOutRequest` — rejected because it would let a compromised or buggy client claim
PIN-verified identity was skipped when the location actually requires it (or vice versa); the
location's own setting must be the single source of truth, read fresh from the database on
every request.

## R2 — Request contract change

**Decision**: `CheckInRequest`/`CheckOutRequest`'s `Pin` field changes from `string` to
`string?`. When `RequiresCaregiverPin` is `false` for the location, the server ignores whatever
value (if any) arrives in `Pin` and proceeds without verification. When `true`, a `null`/empty
`Pin` is rejected exactly as an invalid PIN is today.

**Rationale**: No new request field is needed — the branch is entirely a function of the
location's stored setting, not a client instruction, per R1.

**Alternatives considered**: Adding a `SkipPin: bool` field to the request — rejected as
redundant with the location setting already being the authority, and it would reintroduce the
client-trust problem R1 explicitly rules out.

## R3 — Surfacing the setting to the caregiver tablet

**Decision**: Change the roster endpoint's response from a bare `IReadOnlyList<RoomRosterCardResponse>`
to a wrapper `RoomRosterResponse(bool RequiresCaregiverPin, IReadOnlyList<RoomRosterCardResponse> Caregivers)`.
`GetRoomRosterQueryHandler` loads the location's `RequiresCaregiverPin` alongside its existing
staff/shift queries and includes it in the response.

**Rationale**: The feature description explicitly calls for fetching the flag "alongside the
existing roster call ... rather than a separate request." The roster call already runs once per
room-home-screen load/refresh, so this adds one extra column read with no new round trip.

**Alternatives considered**: A separate `GET /locations/{id}` call from the tablet — rejected,
adds a network round trip the tablet doesn't otherwise need and duplicates data the roster call
already touches (location scoping).

## R4 — Mobile UI behavior

**Decision**: `mobile/app/(room)/index.tsx` reads `requiresCaregiverPin` from the roster
response. When `false`, tapping an unchecked-in/checked-in card calls `checkIn`/`checkOut`
directly with `pin: undefined` and skips mounting `PinKeypad` entirely. When `true`, existing
behavior (mount `PinKeypad`, submit with the entered PIN) is unchanged.

**Rationale**: Matches FR-008 and the feature description's stated preference ("no keypad shown
at all" over "a keypad that accepts anything").

**Alternatives considered**: Always show the keypad but auto-submit/bypass its result — rejected,
adds a pointless UI step and risks a caregiver assuming the keypad's input still matters.

## R5 — `confirmAdministrator` behavior

**Decision**: No change to `ConfirmAdministratorCommand` or `AdministratorConfirmation.tsx`.
Resolved via `/speckit-clarify` (see spec.md Clarifications): this step always requires PIN
verification (or explicit Skip), independent of `Location.RequiresCaregiverPin`.

**Rationale**: It is a distinct, higher-bar verification step for medical/sensitive actions, and
already has its own bypass (`Skip` → `administered_by = null`). This feature's setting is scoped
to routine check-in/check-out only, per FR-013.

## R6 — Web settings screen placement

**Decision**: A new tab on `/locations/[id]`, alongside "Algemeen" and "Reserveringsinstellingen"
— not an addition to the existing `GeneralLocationForm`. New component
`web/components/CheckInSettingsForm.tsx`, following `ReservationSettingsForm.tsx`'s shape (local
state seeded from `location.requiresCaregiverPin`, `PUT` to a dedicated sub-resource endpoint,
success/error `notice` string, calls `onSaved`).

**Rationale**: `GeneralLocationForm` is documented as deliberately minimal (core physical/contact
details only — it doesn't even expose the existing `FlexPermission`/`BoPermission` toggles). This
setting needs its own explanatory tradeoff copy (FR-003), which doesn't fit that tab's minimal
framing. `ReservationSettingsForm` (013f) already established the pattern of a dedicated tab for
a distinct per-location policy decision with its own explanatory framing — this feature follows
that precedent rather than inventing a new one.

**Alternatives considered**: Extending "Algemeen" — rejected per the above; a single unlabeled
checkbox there would undersell the tradeoff this setting carries. Tab label: "Inchecken" (check-in),
naming the workflow area the setting governs, consistent with 013f's Dutch-language tab-naming
convention ("Reserveringsinstellingen").

## R7 — Data migration

**Decision**: One EF Core migration adding `RequiresCaregiverPin boolean NOT NULL DEFAULT true` to
`tenant_template.locations`, following `20260711204852_AddLocationReservationSettings.cs`'s
`migrationBuilder.AddColumn<bool>(...)` shape. Per constitution VI, this migration is authored/
reviewed as normal code and is NOT auto-applied to existing tenant schemas — it goes through the
existing manual per-tenant rollout process (`migrate-tenants` CLI command, feature 002).

**Rationale**: Standard schema-change process already established by every prior
Location-extending feature (004, 013f). Default `true` preserves current behavior for every
existing location until a director explicitly opts out (SC-004).
