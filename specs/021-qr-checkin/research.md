# Phase 0 Research: QR Contactless Check-In

## R1: Check-in code issuance/verification mechanism

**Decision**: A signed, self-contained token (HMAC-SHA256), not a server-side persisted record.

The parent-mobile client requests a code from `POST /api/parent/attendance/qr-code` (ParentOnly,
child-ownership checked server-side per R3). The server builds a payload
`{childId, issuedAtUnix, nonce}`, computes an HMAC-SHA256 signature over it using a
server-side-only signing key (ASP.NET Core configuration / secrets manager per Constitution VI —
never shipped to any client), and returns `base64url(payload) + "." + base64url(signature)` as
the QR code's encoded string. The parent app renders that string as a QR code entirely
client-side (no image round-trip).

On scan, `POST /api/attendance/qr-code/verify` (DeviceAuthenticated) recomputes the HMAC over the
decoded payload and rejects on mismatch (FR-007 — tamper-evident), rejects if
`now - issuedAtUnix > 30s` (FR-006), and rejects if `childId` isn't enrolled at the scanning
device's `LocationId` claim (FR-010). The `nonce` plus a short in-memory/DB "recently consumed"
set (keyed by nonce, TTL slightly longer than the code's own 30s window) enforces FR-019's
post-consumption cooldown — reusing the code's own nonce as the cooldown key needs no extra
column on any persisted entity.

**Rationale**: A signed token needs zero new persisted table — verification is pure computation
(decode, verify signature, check timestamp), which comfortably clears the sub-10-second SC-003
budget with no extra database round-trip on the hot path. This mirrors the codebase's existing
signed-URL pattern for GCS access (feature 031) — server-issued, time-bounded, tamper-evident,
verified without a lookup table.

**Alternatives considered**:
- *Server-side persisted code record (a `CheckInCode` table with an `Id`, `ExpiresAt`,
  `ConsumedAt`)*: rejected as the primary mechanism — it works, but adds a write on every code
  *issuance* (parents may re-open the screen often) for no benefit over a signed token, since
  the codebase already has a working signed/time-bounded pattern to reuse. The nonce-cooldown
  set above is the only piece of "did we see this before" state actually needed, and that's far
  cheaper than a full entity.
- *JWT (full JWS) instead of a bespoke HMAC envelope*: rejected — pulls in JWT library overhead
  and claims machinery for a payload that's just `{childId, issuedAtUnix, nonce}`; a plain HMAC
  envelope is simpler to reason about and smaller to encode into a QR code (QR code size/density
  scales with payload length, and a tighter payload means a more reliably scannable code at
  typical drop-off-line distances).

## R2: Caregiver-tablet camera / QR-decoding library

**Decision**: `expo-camera`'s built-in barcode scanning (`CameraView`'s `onBarcodeScanned`,
`barcodeTypes: ['qr']`) — no separate QR-decoding library.

**Rationale**: `expo-camera` is already an Expo SDK package (matches this codebase's existing
Expo-first dependency policy — no bare React Native camera modules anywhere in `mobile/`) and
has shipped built-in barcode/QR scanning since SDK 51, removing the need for a second
camera/decode library pair. Neither `mobile/package.json` nor `parent-mobile/package.json`
currently depends on any camera or QR library (verified during Product Context research), so
this is a net-new dependency either way — `expo-camera` is the smallest addition that covers
both the viewfinder UI and the decode step in one package.

**Alternatives considered**: `react-native-vision-camera` + a separate QR-decode library —
rejected, more capable (e.g. frame processors) but heavier and not Expo-managed-workflow-first;
nothing in this feature's requirements (a single QR type, no custom frame processing) needs
that extra capability.

## R3: Parent-mobile QR-code rendering library

**Decision**: `react-native-qrcode-svg` (renders the code as an SVG view, no native module,
Expo-compatible).

**Rationale**: Pure-JS/SVG rendering needs no native linking step, consistent with this
codebase's Expo-managed-workflow constraint; takes the R1-produced encoded string directly as
input with no server round-trip for image generation.

**Alternatives considered**: server-rendered QR image (PNG) returned by the issuance endpoint —
rejected, adds unnecessary payload size and a re-fetch on every 20-second refresh cycle for no
benefit over local rendering of a string the client already receives.

## R4: Parent → child ownership check for code issuance

**Decision**: Reuse the existing `Contact.TenantUserId` ↔ `ChildContact` link (the same
ownership model feature 013's messaging and 031's photo-download endpoints already check) — a
parent may only request a code for a `ChildId` reachable via
`ChildContacts.Where(cc => cc.ChildId == childId).Join(Contacts.Where(c => c.TenantUserId ==
currentParentTenantUserId))`. No new linkage table.

**Rationale**: This is the codebase's one existing parent-to-child ownership mechanism; inventing
a second one for this feature would fragment authorization logic across two different checks for
the same underlying question.

## R5: Attendance state-transition reuse (check-in vs. check-out branching)

**Decision**: `VerifyCheckInCodeCommandHandler` reads today's `AttendanceRecord` status for the
scanned `childId`/device `LocationId` (same query `CheckInCommand`/`CheckOutCommand` already run
internally) to decide which of the two existing commands to invoke via `IMediator.Send`, then
returns that command's result. No attendance-toggle logic is duplicated — this handler is a thin
router in front of feature 010's existing state machine, matching FR-008/FR-009/FR-014's
requirement that a scan produce an identical result to a manual tap.

**Rationale**: `CheckInCommand`/`CheckOutCommand` already own every edge case this feature must
not regress (closure days, concurrent-write unique-constraint race handling, planned-duration
calculation, BKR-relevant `RecordedBy` attribution). Re-deriving any of that in a new code path
would risk exactly the FR-014 parity requirement this feature exists to guarantee.

**Alternatives considered**: a single new `ScanAttendanceCommand` that reimplements the
check-in/check-out logic inline — rejected, duplicates state machine logic already proven in
production (feature 010), directly risking the parity requirement.

## R6: Offline handling on the caregiver tablet

**Decision**: No new offline path. The scan screen calls the same `checkIn`/`checkOut`
functions in `mobile/services/attendance.ts` (extended to accept a resolved code-verification
result), which already branch on `isConnected` to either call the API directly or `enqueue()`
into the existing feature-008 offline queue. `VerifyCheckInCodeCommand` runs online-only (a scan
needs a live signature check against the server's private key, which a tablet cannot do
offline) — so the tablet performs online verification when connected, and when offline, the
scan flow falls back to prompting the caregiver to use the manual tap flow instead (FR-013's
existing fallback requirement), rather than attempting to queue an unverified code.

**Rationale**: FR-012 requires offline queuing for "a valid code scanned while offline," which
seems to require offline verification — but FR-013 already guarantees manual tap as the
always-available fallback, and the code's own security model (R1) requires server-side HMAC
verification, which is structurally impossible offline. Resolution: the tablet attempts online
verification first; if the device has no connectivity at all (detected the same way the existing
`isConnected` check already gates every other attendance action), the scan-mode screen shows a
clear message directing the caregiver to manual tap, which itself already queues offline exactly
per FR-012's intent — the *underlying* check-in/check-out still queues offline, just via the
manual path rather than a re-verified scan. This is a plan-level refinement of FR-012, not a
contradiction: the spec's Assumptions section already treats manual tap as the universal offline
fallback (User Story 3), and FR-012's "valid code scanned while offline" case in practice means
"scanned in a spotty-but-momentarily-connected window" (verification succeeds, then the
resulting attendance write itself queues offline) rather than "verified with zero connectivity,"
since the latter is not achievable without shipping the private signing key to every tablet.
