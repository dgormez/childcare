# Contract: QR Contactless Check-In API

Three new endpoints: a director-facing location setting (`DirectorOnly`), a parent-facing code
issuance endpoint (`ParentOnly`), and a caregiver-tablet-facing verification endpoint
(`DeviceAuthenticated`) — each reuses this codebase's existing authorization policies, no new
policy invented.

## `PUT /api/locations/{locationId}/qr-checkin-setting`

`DirectorOnly`. Mirrors feature 008b's `UpdateLocationCheckInSettingsCommand` pattern (a sibling
setting on the same entity, not a reuse/rename of it).

Request:
```json
{ "enabled": true }
```

- Updates `Location.QrCheckInEnabled` (FR-001).
- Emits a structured log entry (director id, location id, old/new value, timestamp — FR-016),
  only when the value actually changes (mirrors 008b's handler, which logs only on change).
- `200`: the full, updated `LocationResponse` — this endpoint follows the same
  update-and-return-the-whole-entity convention as every other `PUT /api/locations/{id}/...-
  settings` route (`checkin-settings`, `reservation-settings`, etc.), not a bespoke narrow shape.
- `404 errors.locations.not_found` — `locationId` doesn't resolve within this tenant.
- `403` — caller isn't a director for this tenant (existing `DirectorOnly` policy).

## `POST /api/parent/attendance/qr-code`

`ParentOnly`. Issues a signed, 30-second-lived code for one child (research.md R1).

Request:
```json
{ "childId": "guid" }
```

- `400 errors.qrCheckIn.not_enabled` — the child's enrolled location has `QrCheckInEnabled =
  false` (FR-004 — no QR entry point should be reachable at a disabled location; the client is
  expected not to surface this screen at all when disabled, this is defense in depth).
- `403 errors.qrCheckIn.not_your_child` — `childId` isn't reachable via the calling parent's
  `Contact.TenantUserId` → `ChildContact` link (research.md R3/R4) — a parent can never issue a
  code for a child they aren't linked to.
- `404 errors.children.not_found` — `childId` doesn't resolve within this tenant.
- `200`:
  ```json
  { "code": "base64url(payload).base64url(signature)", "expiresAtUnix": 1721472930 }
  ```

## `POST /api/attendance/qr-code/verify`

`DeviceAuthenticated` (same policy/claims-resolution as `/api/attendance/check-in`,
`DeviceTokenRotationFilter` applied). `LocationId`/`GroupId` resolved from the scanning tablet's
own device-token claims, never client-supplied (same convention as every other
`DeviceAuthenticated` attendance endpoint).

Request:
```json
{ "code": "base64url(payload).base64url(signature)" }
```

Verification order (first failure wins):
1. Decode + verify HMAC signature → `401 errors.qrCheckIn.invalid_code` on mismatch or malformed
   input (FR-007).
2. Check nonce not already in the consumed-cooldown set → `409 errors.qrCheckIn.already_used`
   (FR-019).
3. Check `now - issuedAtUnix <= 30` → `410 errors.qrCheckIn.code_expired` (FR-011).
4. Check the code's `childId` is enrolled at the scanning device's `LocationId` claim →
   `403 errors.qrCheckIn.wrong_location` (FR-010).
5. Look up today's `AttendanceRecord` status for `childId`/device `LocationId` and dispatch to
   `CheckInCommand` (not currently present/no record) or `CheckOutCommand` (currently present) —
   research.md R5. Record the nonce in the consumed-cooldown set immediately after a successful
   dispatch (FR-019).
- `200`:
  ```json
  {
    "attendance": { /* AttendanceRecordResponse — identical shape to a manual tap, FR-008 */ },
    "direction": "check-in" | "check-out",
    "childFirstName": "string",
    "childLastName": "string",
    "childPhotoDownloadUrl": "string | null"
  }
  ```
  `attendance` is the underlying `CheckInCommand`/`CheckOutCommand` result, untouched.
  `direction` lets the tablet pick the right confirmation copy without re-deriving it from the
  record's own before/after state. `childFirstName`/`childLastName`/`childPhotoDownloadUrl` let
  the scan confirmation show the child's name/photo (User Story 2's acceptance scenarios)
  without the tablet needing a second lookup.
- Any failure from the underlying `CheckInCommand`/`CheckOutCommand` (e.g. closure day) surfaces
  with that command's existing error key — no new error shape invented for cases already handled
  by feature 010.

## Client offline behavior (both tablet and parent app)

- **Parent app**: code issuance requires connectivity — no offline queuing (a code cannot be
  meaningfully pre-generated without the server's signing key). Shows a "reconnect to show your
  code" state when offline (spec.md UX Requirements).
- **Caregiver tablet**: verification requires connectivity (research.md R6). A fully offline
  tablet (checked before attempting a scan) shows a message directing the caregiver to manual
  tap rather than attempting one (FR-012's first clause). Verification and its resulting
  `CheckInCommand`/`CheckOutCommand` write happen atomically in one server-side request — there
  is no separate client-visible "verified, write pending" step to queue. FR-012's second clause
  (connectivity lost *during* an in-flight request the tablet believed was online) is instead
  handled by distinguishing that case at the client: `mobile/services/attendance.ts`'s
  `scanCheckInCode` catches a thrown/network-level failure separately from a genuine HTTP
  rejection and surfaces a dedicated `errors.qrCheckIn.connection_lost`, shown as "check the
  roster before scanning again" rather than implying the code itself was invalid — since the
  server may or may not have completed the write, and replaying the same (now possibly
  already-consumed) code isn't safely idempotent the way replaying a queued manual tap is.
