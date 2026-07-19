# Feature Specification: QR Contactless Check-In

**Feature Branch**: `021-qr-checkin`

**Created**: 2026-07-19

**Status**: Draft

**Input**: User description: "QR contactless check-in — parent shows a QR code on their phone,
caregiver tablet scans it, no manual staff tap needed at drop-off, replacing the attendance tap
with a scan for locations that opt in. This must be a per-location setting that a director
manages in the web admin (director) platform, and it MUST be disabled by default for every
location until a director explicitly turns it on."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director enables QR check-in for a location (Priority: P1)

A director opens a location's settings in the web admin and turns on "QR-inchecken toegestaan".
Explanatory copy makes clear what this adds: parents at that location will be able to show a
code on their phone instead of the caregiver tapping a manual entry, but the caregiver still
physically receives the child — no change to the handover ritual itself. Until a director does
this, the location behaves exactly as it does today, with no QR entry point visible anywhere.

**Why this priority**: This is the gate the whole feature sits behind — without it, nothing else
in this spec can be exercised, and no existing location's behavior may change the moment this
feature ships.

**Independent Test**: Can be fully tested by a director toggling the setting on for one location,
saving, and verifying only that location gains a QR entry point — a second, untouched location
shows none.

**Acceptance Scenarios**:

1. **Given** a director is viewing a location's settings, **When** they open the relevant tab,
   **Then** they see the current state of the QR check-in setting (defaulting to "disabled" for
   any location that has never changed it) alongside copy explaining what turning it on adds.
2. **Given** the director turns the setting on and saves, **When** the save completes, **Then**
   the location's QR check-in setting is persisted as enabled and every other location's setting
   is unaffected.
3. **Given** the setting is on, **When** the director turns it back off and saves, **Then** the
   QR entry point disappears from both the parent app and the caregiver tablet for that location,
   and manual tap-based check-in continues to work exactly as before.

---

### User Story 2 - Parent and caregiver complete check-in/check-out via QR scan (Priority: P1)

At a location where QR check-in is enabled, a parent opens a screen in the parent app showing a
code for their child. At drop-off, the caregiver opens scan mode on the room tablet and scans the
parent's code. The caregiver still physically receives the child — the scan replaces the manual
attendance tap, not the handover itself. A successful scan shows the child's name and photo with
a clear "checked in" confirmation, then the tablet returns to scan mode. The same flow, scanned a
second time, checks the child back out at pickup.

**Why this priority**: This is the actual time-saving experience the setting exists to produce —
without it, User Story 1's toggle has no observable effect for the people it's meant to help
during busy drop-off windows.

**Independent Test**: Can be fully tested at an enabled location by scanning a parent's code and
observing the child is checked in with no manual tap, then scanning the same code again and
observing the child is checked out.

**Acceptance Scenarios**:

1. **Given** an enabled location and a child not currently checked in, **When** the caregiver
   tablet scans that child's parent's code, **Then** the child is checked in immediately, the
   tablet shows a success confirmation with the child's name and photo, and then returns to scan
   mode.
2. **Given** the same child is now checked in, **When** the caregiver tablet scans the same
   parent's code again, **Then** the child is checked out immediately with the same style of
   confirmation.
3. **Given** a scanned code identifies a child not enrolled at the scanning tablet's location,
   **When** the scan is processed, **Then** it is rejected with a clear, human-readable message
   and no attendance record is created.
4. **Given** a parent's displayed code has expired before it is scanned, **When** the caregiver
   tablet scans it, **Then** the scan is rejected with a clear "code expired" message, and the
   parent app refreshes to a valid code the next time it is opened or brought to the foreground.
5. **Given** the caregiver tablet has no network connectivity at the moment of a valid scan,
   **When** the scan is processed, **Then** the resulting check-in/check-out is queued using the
   existing offline mechanism and reconciled once connectivity returns, exactly as a manual tap
   would be.

---

### User Story 3 - Manual tap-based check-in remains available and unaffected everywhere (Priority: P2)

Whether or not a location has QR check-in enabled, and even at an enabled location if a parent's
phone or the tablet's camera isn't usable in the moment, a caregiver can always fall back to the
existing manual tap-based check-in/check-out flow with no degradation.

**Why this priority**: QR check-in is additive convenience, not a safety-critical path — the
feature must never become a point of failure for getting children checked in.

**Independent Test**: Can be fully tested by disabling the tablet camera (or being at a disabled
location) and confirming manual tap-based check-in completes exactly as it does today.

**Acceptance Scenarios**:

1. **Given** a location has QR check-in disabled, **When** a caregiver checks a child in or out,
   **Then** the experience is identical to today's manual tap flow, with no QR-related UI shown
   anywhere.
2. **Given** a location has QR check-in enabled but the tablet's camera is unavailable or fails,
   **When** a caregiver needs to check a child in or out, **Then** they can complete the action
   via the existing manual tap flow without being blocked.

---

### Edge Cases

- A parent has more than one child enrolled (feature 030, not yet shipped): until that feature
  exists, this feature treats each child independently — a parent with multiple children sees a
  code per child rather than one combined code. The specific selection UI for choosing between
  children is deferred to feature 030 and is not designed here.
- A director disables QR check-in mid-day while parents/caregivers are actively relying on it.
  Any check-in/out already completed via QR before the change is unaffected. The QR entry point
  disappears for the next attempt at that location; manual tap remains available throughout.
- A parent's code is captured (e.g., a photo of the phone screen) and reused later. The code's
  short validity window is the primary defense; a code that has expired by the time of reuse must
  be rejected per User Story 2's acceptance scenario 4.
- A caregiver scans a code for a child who is already checked in in a way that doesn't fit the
  simple in/out toggle (e.g., a second scan arrives before the first has finished processing) —
  the system must not create duplicate or conflicting attendance records; the second scan is
  treated as the current authoritative state transition, consistent with how the existing manual
  tap flow already handles rapid repeated taps.
- QR check-in must never bypass or weaken the caregiver's physical receipt of the child — this
  feature only changes how the attendance record is created, never the handover ritual itself.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Directors MUST be able to view and change, per location, whether QR check-in is
  enabled.
- **FR-002**: The QR check-in setting MUST default to disabled for every location, including all
  locations that existed before this feature shipped, so no location's behavior changes without a
  deliberate director action.
- **FR-003**: The director-facing setting screen MUST present explanatory copy, in plain
  operational language, describing what enabling the setting adds (a scan-based alternative to
  the manual attendance tap) and confirming the handover ritual itself is unchanged.
- **FR-004**: When a location's QR check-in setting is disabled, no QR entry point MUST be shown
  in the parent app or on the caregiver tablet for that location, and check-in/check-out MUST
  work exactly as it does today via manual tap.
- **FR-005**: When a location's QR check-in setting is enabled, the parent app MUST display a
  code, scoped to one child and one parent, that a caregiver's tablet can scan to identify that
  child.
- **FR-006**: Each displayed code MUST expire after a short, fixed validity window and MUST be
  refreshed automatically before or upon expiry so a parent does not need to manually request a
  new one under normal use.
- **FR-007**: Each code MUST be tamper-evident — the system MUST reject any code that has been
  altered or was not issued by the system itself, so a forged or guessed code cannot produce a
  check-in/check-out.
- **FR-008**: A successful scan MUST produce the same underlying attendance record (child,
  location, caregiver present, timestamp) that a manual tap produces today, so every downstream
  reader of that record (staffing-ratio calculation, reporting) treats it identically regardless
  of origin.
- **FR-009**: The system MUST determine check-in vs. check-out by the child's current attendance
  state at scan time: scanning a code for a child not currently checked in performs a check-in;
  scanning again performs a check-out — mirroring the existing manual tap toggle behavior.
- **FR-010**: A scan for a child not enrolled at the scanning tablet's location MUST be rejected
  with a clear, human-readable message, and MUST NOT create any attendance record.
- **FR-011**: A scan of an expired code MUST be rejected with a clear, human-readable message
  distinct from the "wrong location" message in FR-010.
- **FR-012**: If the caregiver tablet has no network connectivity when a valid code is scanned,
  the resulting check-in/check-out MUST be queued via the existing offline mechanism (feature
  008) and reconciled once connectivity returns, exactly as a manual tap would be.
- **FR-013**: If the tablet's camera or scanning capability is unavailable or fails, the caregiver
  MUST still be able to complete check-in/check-out via the existing manual tap flow — QR check-in
  MUST always be additive, never a replacement that can block attendance.
- **FR-014**: The legally-required staffing-ratio calculation and any event/incident attribution
  that depends on attendance state MUST produce identical results whether a given check-in/
  check-out originated from a QR scan or a manual tap.
- **FR-015**: Turning QR check-in on or off for a location MUST NOT affect any attendance record
  already created before the change — only check-ins/check-outs attempted after the change are
  governed by the new setting value.
- **FR-016**: Every change to this setting MUST produce a structured server-side log entry
  recording which director changed it, which location, the old and new value, and when.
- **FR-017**: All new user-facing text introduced by this feature (setting label, explanatory
  copy, scan confirmation, rejection messages) MUST be available in Dutch, French, and English via
  the existing i18n mechanism — no hardcoded strings.
- **FR-018**: If saving the setting fails (network error, validation error, or the director is no
  longer authorized), the director MUST see a clear, human-readable error and the toggle MUST
  revert to its last-saved state — never silently appear changed when the save did not succeed.

### Key Entities

- **Location** (existing entity, extended): gains a single setting recording whether QR check-in
  is enabled for that location. Defaults to disabled.
- **Check-In Code** (new, ephemeral): a short-lived, tamper-evident code scoped to one child and
  one parent, generated by the parent app and consumed by a caregiver tablet's scan. Not a
  persisted attendance record itself — only the successful scan produces one.
- **Attendance Record** (existing entity, from feature 010, unchanged): the check-in/check-out
  record produced identically whether triggered by a QR scan or a manual tap; this feature adds
  no new field to it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can change a location's QR check-in setting and see the change reflected
  in the toggle state within the same settings visit, with no page reload needed to confirm the
  save succeeded.
- **SC-002**: Zero existing locations show any QR entry point immediately after this feature
  ships — the default preserves today's manual-tap-only behavior everywhere until a director
  explicitly opts in.
- **SC-003**: At an enabled location, a caregiver completes a child's check-in via scan in under
  10 seconds from tablet scan-mode to confirmation shown, with zero manual data entry.
- **SC-004**: 100% of QR-originated attendance records produce identical staffing-ratio and
  event/incident-attribution results compared to manually-tapped records, in side-by-side
  comparison testing.
- **SC-005**: At a location with QR check-in disabled, or when the camera path fails at an
  enabled location, 100% of check-ins/check-outs can still be completed via the existing manual
  tap flow with no additional steps.

## Assumptions

- Directors are the only role able to change this setting, consistent with every other
  per-location setting in this system (mirrors feature 013f and 008b's precedent).
- The per-location setting is surfaced to the caregiver tablet via the existing roster/settings
  fetch mechanism (feature 008a), consistent with how the tablet already learns per-location
  facts today; a tablet offline at the moment a director changes the setting continues operating
  on its last-fetched value until its next successful sync.
- Multi-child parents (feature 030, not yet built) are out of scope for designing a combined or
  switcher UI in this feature; each child is handled independently in the interim.
- No NFC, badge, or physical door-hardware integration (e.g. Paxton/Net2) is in scope — those
  remain a later-phase concern per the product backlog.
- A physical QR sticker (e.g. on a child's bag) is out of scope — the code is only ever displayed
  live in the parent app, never a static, reusable artifact.
- The specific cryptographic/token mechanism used to make codes tamper-evident and short-lived is
  an implementation decision for the planning phase, not specified here.
