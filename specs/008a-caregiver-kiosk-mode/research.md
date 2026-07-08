# Phase 0 Research: Caregiver App Kiosk Mode

## R1: Device-token auth composes with `TenantMiddleware` via a policy-scheme forwarder, not code changes

**Decision**: Register a second, named JWT bearer scheme (`"DeviceToken"`) alongside the
existing default `"Bearer"` scheme, each with its own issuer/audience/signing key so a user
token and a device token can never be mistaken for one another. Make the *actual* default
scheme a `AddPolicyScheme("Bearer", ..., options => options.ForwardDefaultSelector = ctx => ...)`
that cheaply inspects the incoming token's `iss` claim (decode-without-validate, just to route)
and forwards to either the real user-JWT scheme or the `DeviceToken` scheme.

**Rationale**: `TenantMiddleware` (feature 002) reads `context.User.FindFirst("tenant_id")`
directly — it has no idea which authentication scheme populated `HttpContext.User`, and it
shouldn't need to. `UseAuthentication()` only auto-populates `HttpContext.User` from whichever
scheme is the pipeline's *default* — without the forwarder, a request carrying only a device
token (no user JWT) would reach `TenantMiddleware` as anonymous, since the default scheme
wouldn't recognize it. The device token already carries `tenant_id` (spec: `{ tenant_id,
location_id, group_id, device_id }`), so once the forwarder routes correctly, `TenantMiddleware`
works completely unchanged for device-authenticated requests. This mirrors the existing
`SuperAdminAuthenticationHandler` precedent (a second, non-default scheme opted into via
policy) but goes one step further since device-authenticated requests need to reach
`TenantMiddleware` too, unlike super-admin requests which are `TenantExempt`.

**Alternatives considered**:
- *Modify `TenantMiddleware` to explicitly try both schemes*: rejected — couples a
  feature-002 file to feature-008a's auth mechanism, and every future third credential type
  would need the same edit again. The forwarder pattern scales to N schemes with zero changes
  to `TenantMiddleware`.
- *Single shared JWT scheme, device tokens as just another JWT with a `token_type` claim*:
  rejected — deliberately keeping user and device tokens on separate signing keys means a
  compromise of one signing key can't be used to forge the other credential type, and makes
  "reject every device token, keep user sessions alive" (or vice versa) a one-line
  incident-response change if ever needed.

## R2: PIN lockout is a simple per-`StaffProfile` counter, not the ASP.NET Core rate-limiter middleware

**Decision**: Add `PinFailedAttempts` (int), `PinFirstFailedAttemptAt` (`DateTime?`),
`PinLockedUntil` (`DateTime?`) directly to `StaffProfile`. `VerifyPinCommand` (the single shared
PIN-check path used by check-in, check-out, and sensitive-action confirmation — spec
Clarifications) always receives an explicit `staff_id` alongside the PIN (select-then-PIN — spec
User Story 3/BACKLOG.md's revision), loads that one `StaffProfile`, checks/updates its own
lockout fields, and bcrypt-compares the PIN against its own `PinHash`. On failure it increments
the sliding-window streak (anchored to the first failure, not a fixed clock window) and sets
`PinLockedUntil` at 5 failures within 2 minutes; a successful match resets it.

**Rationale, and why this is simpler than an earlier draft of this decision**: an earlier draft
of this feature had the caregiver identify themselves by PIN alone (server searches for who it
might belong to), which made per-`StaffProfile` lockout structurally impossible — a wrong guess
never resolved to one candidate, so there was no row to charge the failure to, and a
value-keyed table (HMAC lookup hash) was the fix. That premise changed: the caregiver now taps
their own photo card first (select-then-PIN, the industry-standard pattern for small, known
staff pools — confirmed against Procare and KinderSign), so every PIN-verifying call already
names its target via `staff_id`. With the target always known up front, a per-`StaffProfile`
counter is not just viable again, it's the simplest thing that satisfies FR-012's "a different
caregiver's card is unaffected" requirement — no HMAC, no extra table, no value-keying needed.

The existing `AddRateLimiter`/`SlidingWindowLimiter` infrastructure (`Program.cs`) partitions by
client IP — appropriate for protecting the server from request floods, but wrong for this
requirement, which is about locking one specific *credential* (a caregiver's PIN) regardless of
which IP is trying it, for a fixed 10-minute window rather than a rolling rate. This has to live
in the command handler next to the bcrypt comparison, not in HTTP-layer middleware.

**Alternatives considered**:
- *A value-keyed `pin_lockouts` table (HMAC lookup hash of the submitted digit string)*: this
  feature's original design, when the caregiver was identified by PIN alone with no `staff_id`.
  No longer necessary now that `staff_id` is always explicit — removed in favor of the simpler
  per-`StaffProfile` shape.
- *Device-wide lockout (any 5 wrong guesses of any caregiver locks the whole tablet)*: rejected
  — contradicts FR-012's explicit "a different caregiver's card is unaffected" acceptance
  criterion; would lock out every caregiver in the room over one caregiver's fat-fingering.

## R3: Device-token rotation is an endpoint filter, not a MediatR command

**Decision**: An `IEndpointFilter` applied to the device-authenticated route group inspects the
validated token's expiry claim after the request completes; if fewer than 7 days remain, it
mints a replacement device token and adds it as an `X-Device-Token-Refresh` response header.
The mobile client swaps it into `SecureStore` on any response that carries the header.

**Rationale**: Rotation isn't a user-initiated action — there's no meaningful "rotate token"
command a caregiver or director triggers, it's a side effect of *any* authenticated request
crossing the threshold. An endpoint filter is the idiomatic place for a cross-cutting concern
that applies uniformly to a route group, consistent with how `TenantMiddleware` itself is a
cross-cutting concern rather than something each handler calls explicitly.

**Alternatives considered**:
- *Rotate on every request*: explicitly rejected by the spec itself (FR-020/US6) — would break
  offline-queue replay bursts, since the first replayed request would invalidate the token the
  remaining 29 queued requests still carry.
- *A background job that proactively rotates tokens nearing expiry*: rejected — requires a
  reliably-running scheduled process, which this project doesn't have wired up yet (see R5),
  for no benefit over the simpler request-triggered approach the spec already describes.

## R4: No synthetic test-only endpoint needed for FR-014–016

**Decision**: Prove "device token alone is sufficient auth, no individual login gate" directly
via the real `POST /api/room-shifts/check-in`/`check-out` endpoints (they require only a valid
device token, no PIN-as-HTTP-auth — `staff_id`/`pin` are request-body content, not a
credential).
Prove `recorded_by`/`administered_by` attribution directly against `IShiftAttributionService`,
integration-tested with seeded `room_shifts` rows in a real TestContainers database — no HTTP
round-trip needed to test a plain injectable service.

**Rationale**: Feature 008's sync engine needed a synthetic `_test_entity` because its entire
job was "replay *any* entity type generically" — there was no real entity to test against yet,
and the mechanism itself was the product. Here, the reusable piece is a plain C# service with a
narrow, directly-testable contract (`ResolveRecordedBy(locationId, groupId, occurredAt) ->
Guid[]`) — testing it doesn't require inventing a fake HTTP endpoint that would need to be
stripped out later. Feature 009 will call the same service from its own real command handlers.

**Alternatives considered**:
- *A `POST /api/_test/device-actions` endpoint, `Testing`-environment-only*: rejected as
  unnecessary scaffolding once it became clear the real check-in/check-out endpoints already
  exercise the exact claim being tested (device-token-only auth), and attribution logic doesn't
  need an HTTP layer to be tested meaningfully.

## R5: Auto-checkout is lazy materialization, not a scheduled job

**Decision**: `GetRoomRosterQuery`, `CheckInCommand`, and `CheckOutCommand` each begin by
closing any shift still open from a *previous* calendar day (local midnight boundary) before
doing their own work — setting `checked_out_at` to that midnight boundary and persisting it.
No background process, no cron.

**Rationale**: This project has no scheduled-job infrastructure yet (no Cloud Scheduler wiring,
no `IHostedService` precedent) and Cloud Run's scale-to-zero model makes a naive in-process
timer unreliable anyway (an idle instance can be killed before a timer fires). A childcare room
tablet is realistically touched constantly during operating hours, so lazy materialization on
the next read/write closes stale shifts essentially immediately in practice, while completely
avoiding new infrastructure for what the spec itself calls an edge case. The persisted
`checked_out_at` still gives directors something real to correct afterward (FR-023).

**Alternatives considered**:
- *A GCP Cloud Scheduler-triggered internal endpoint, run nightly*: the "correct" long-term
  answer, but adds new Terraform-managed infrastructure for a single edge case this feature
  doesn't need to solve precisely at midnight — revisit if a future feature needs real
  scheduled jobs for other reasons, and fold this in then.

## R6: Select-then-PIN — the client identifies the caregiver (`staff_id`), the server only verifies

**Decision**: `VerifyPinCommand` takes `(locationId, staffId, pin)`. It loads exactly one
`StaffProfile` by `staffId`, confirms it's active (not deactivated) and eligible at
`locationId` (`StaffLocationEligibility`) — rejecting `403` outright if either check fails,
*before* touching the PIN at all — then bcrypt-compares the submitted PIN against that one
record's `PinHash`. There is no search, no candidate set, no iteration.

**Rationale, and why this superseded an earlier draft of this decision**: this feature's first
draft had the caregiver enter a PIN with no other context (`{ pin: string }` only), which meant
the server had to *find* the caregiver — since `PinHash` is a salted bcrypt hash with no
queryable plaintext, that required bcrypt-comparing against every candidate eligible at the
device's location. That premise no longer holds: the room home screen now shows every eligible
caregiver as a photo card (FR-013), the caregiver taps their own before entering a PIN, and the
app sends that identity explicitly (select-then-PIN — the industry-standard pattern for small,
known staff pools, confirmed against Procare and KinderSign; PIN-only suits large, anonymous
pools like a parent kiosk, which this isn't). With `staff_id` always given, direct lookup
replaces the whole candidate-set mechanism, which no longer exists in this design.

The eligibility check ahead of the PIN comparison is also what makes FR-004 (device token's
location scope enforced against the request's actual target), FR-024 (deactivated caregiver
rejected), and FR-025 (eligibility re-checked fresh on every call, not cached) fall out of one
piece of logic rather than three separate checks — and, unlike a candidate-set design, it also
means a failed PIN attempt always has an unambiguous target for lockout purposes (research.md
R2's simple per-`StaffProfile` counter).

**Alternatives considered**:
- *PIN-only, server resolves the caregiver by scoped candidate-set bcrypt comparison*: this
  feature's original design. Rejected in favor of select-then-PIN once the product direction
  was confirmed against real-world reference products (Procare, KinderSign) — it's simpler
  (no iteration, no anonymous-failure lockout problem) and matches established UX for
  small/known staff pools.
- *A separate, queryable (non-bcrypt) PIN index*: rejected outright — would mean PINs are no
  longer stored only as an irreversible hash, violating FR-019 and constitution Principle VI.

## R7: The room roster is every location-eligible caregiver, not a group-scoped list

**Decision**: `GET /api/room-shifts/roster` returns every `StaffProfile` eligible at the
device token's `location_id` (`StaffLocationEligibility`, feature 005) — not narrowed further
to the token's `group_id` — each with their existing profile photo (`IProfilePhotoStorage`,
feature 005; a placeholder avatar if unset) and current checked-in state/time (`RoomShift`).

**Rationale**: BACKLOG.md's revision says the roster is "all caregivers assigned to this
group," but there is no `StaffGroupAssignment`-equivalent entity anywhere in this codebase —
staff eligibility is only tracked at the location level (`StaffLocationEligibility`); per-group
staff scheduling is explicitly out of scope until a future feature (`StaffLocationEligibility`'s
own doc comment: "carries no date/schedule information — that belongs to feature 011").
Introducing a new staff-to-group assignment concept purely to satisfy one descriptive phrase in
a backlog entry would be scope creep well beyond this feature's actual auth/presence-tracking
purpose. Location-eligible is the closest existing concept, is a superset (safe direction to
err — showing a caregiver's card who could plausibly work this room, rather than hiding one who
can), and is consistent with every other eligibility check this feature already makes
(FR-004/024/025).

**Alternatives considered**:
- *Add a new `StaffGroupAssignment` entity to narrow the roster to exactly one group*: rejected
  as out of scope — no precedent, no other part of this feature needs it, and feature 011 is
  the documented home for staff-schedule/assignment concepts.
