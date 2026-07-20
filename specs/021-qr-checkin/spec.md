# Feature Specification: QR Contactless Check-In

**Feature Branch**: `021-qr-checkin`

**Created**: 2026-07-19

**Status**: Draft

**Input**: User description: "QR contactless check-in — parent shows a QR code on their phone,
caregiver tablet scans it, no manual staff tap needed at drop-off, replacing the attendance tap
with a scan for locations that opt in. This must be a per-location setting that a director
manages in the web admin (director) platform, and it MUST be disabled by default for every
location until a director explicitly turns it on."

## Product Context

### Feature Type

Mixed — a director-web settings toggle (data-model change on `Location`), a parent-mobile UI addition (live QR code display), and a caregiver-tablet UI addition (scan mode), all backed by a new backend check-in-code issuance/verification capability.

### Primary Consumer

Mixed — Director (enables/disables the setting), Parent (displays the code), Caregiver (scans it). System is a secondary consumer for offline-queue reconciliation (existing feature 008 mechanism, unchanged).

### Workflow Boundary

This is an additive entry point into the existing **Attendance & Presence** workflow (`Workflows/attendance.md`), not a new workflow — it changes *how* an `attendance_record` transition is triggered (scan vs. tap), never the record shape, the BKR ratio calculation, or the caregiver's physical handover responsibility. `Workflows/attendance.md` gets a short cross-reference note (mirroring how feature 031 cross-referenced four other workflows) rather than new top-level content.

- **Actors**: Director (setting owner), Parent (code holder), Caregiver (scanner), System (offline queue reconciliation, code expiry).
- **Actions**: director toggles per-location setting → parent app displays a short-lived per-child code → caregiver tablet scans → system resolves scan to a check-in or check-out exactly as `CheckInCommand`/attendance toggle logic does today for a manual tap.
- **Data Flow**: `Location` gains a boolean setting (mirrors the `UpdateLocationCheckInSettingsCommand`/`RequiresCaregiverPin` pattern from feature 008b — a distinct, unrelated setting on the same entity). A new ephemeral, tamper-evident code (server-issued, short TTL) is generated on request by the parent app and verified server-side on scan; a successful verification calls the same attendance state-transition path feature 010's manual tap already uses, so `AttendanceRecord`, BKR calculation, and reporting are untouched.
- **Outputs**: `AttendanceRecord` check-in/check-out (identical shape to a manual tap), a structured log entry on setting change (mirrors feature 008b's `ILogger` pattern — no dedicated audit-trail subsystem exists in this codebase, confirmed by grep).
- **Cross-platform Impact**: director-web (new location-settings toggle), parent-mobile (new QR-display screen — no QR/camera library present yet in `parent-mobile/package.json`, confirmed), mobile/caregiver-tablet (new scan-mode screen — no camera/barcode library present yet in `mobile/package.json`, confirmed), backend (new code issuance/verification endpoints reusing feature 010's existing check-in/check-out logic).

### User Impact

This enables a caregiver at an opted-in location to check a child in or out with a single scan instead of a manual tap, and a parent to produce the code with no extra steps beyond opening the app — reducing drop-off friction during the busiest window of the day without changing the physical handover or any downstream compliance record.

### UX Requirements

**Persona**: Director (primary, setting); Caregiver (primary, scan flow); Parent (primary, code display).

**Platform**: director-web (location settings tab), mobile/caregiver-tablet (scan mode), parent-mobile (code display screen).

**User job (director)**: "I want to let this location's parents check in by phone instead of requiring a manual tap, without changing anything for locations that don't want this."
**User job (caregiver)**: "I want to check a child in or out in one motion without touching the tablet's keyboard or hunting for the right roster row."
**User job (parent)**: "I want to show something on my phone at drop-off instead of waiting for a staff member to tap a screen."

**Success criteria**: per SC-001–SC-005 in this spec — instant toggle feedback, zero behavior change for non-opted-in locations, sub-10-second scan-to-confirmation, identical BKR/reporting output, and a working manual fallback at all times.

**Main flow (director)**: location settings → "QR-inchecken toegestaan" tab → toggle + explanatory copy → save → inline confirmation, no reload.
**Main flow (caregiver)**: tablet group view → "Scan" quick action (one tap away, per `reference-products.md`'s Brightwheel principle) → camera viewfinder → successful scan shows child name + photo + check-in/check-out confirmation → auto-returns to scan mode.
**Main flow (parent)**: existing child/timeline screen → "Show code" action → live QR code, auto-refreshing before expiry, no manual refresh needed under normal use.

**Loading/empty/error states**: caregiver scan screen shows a distinct, human-readable message for each of the three rejection cases (wrong location, expired code, tamper/invalid) per FR-010/FR-011/FR-007 — never a generic error; parent code screen shows a loading state while a code is (re)issued and a clear retry affordance if issuance fails; director toggle reverts to last-saved state on save failure (FR-018) with a human-readable error, never a silent revert.

**Accessibility**: caregiver's "Scan" quick action and the manual-fallback entry point both meet the 48pt tablet touch-target floor (`platform-rules.md`); the scan viewfinder provides non-color confirmation (name + photo + text state, not a color flash alone) per `design-system.md`'s "never convey semantic state by color alone" rule.

**Offline behavior**: caregiver tablet scan while offline queues via the existing feature 008 offline-sync mechanism exactly as a manual tap does (FR-012) — same conflict/reconciliation semantics, no new offline path invented. Parent code display requires connectivity to issue/refresh a code (a code is a live, server-issued artifact, not a static offline-generatable value, per the Assumptions' "never a static, reusable artifact" note) — the parent screen shows a clear "reconnect to show your code" state when offline, distinct from a scan rejection.

**i18n**: all new strings (setting label/copy, scan confirmation, three rejection messages, parent code screen copy) added to the existing NL/FR/EN locale files per platform (`mobile/i18n`, `parent-mobile/i18n`, `web` i18n convention) — no hardcoded strings, per FR-017. Caregiver/director copy stays in this codebase's operational register; parent-facing copy stays in parent-mobile's warm register (matches feature 031's precedent for register-per-surface).

### Technical Requirements

**API impact**: new endpoints — issue a check-in code (parent-authenticated, scoped to one child), verify/consume a scanned code (caregiver-tablet-authenticated, scoped to the tablet's location). Verification reuses feature 010's existing check-in/check-out state-transition logic (`ChildCare.Application/Attendance/CheckInCommand.cs` or its sibling) rather than duplicating attendance-toggle logic — a scan is just a different trigger into the same command path. Location settings endpoints follow the existing `UpdateLocationCheckInSettingsCommand`-style pattern (a sibling command, not a rename/reuse of 008b's unrelated `RequiresCaregiverPin`).

**Data-model impact**: one new boolean column on `Location` (e.g. `QrCheckInEnabled`), defaulting to `false` for all existing rows (FR-002) — an EF Core migration with a manually-run SQL script per this repo's convention (`.claude/CLAUDE.md`: "EF Core never auto-migrates in production"). The check-in code itself is ephemeral and tamper-evident (signed/HMAC'd token or short-lived server-side record with TTL — the exact mechanism is a plan.md decision per the spec's Assumptions) and is **not** a new persisted business entity; no change to `AttendanceRecord`.

**Security considerations**: codes must be tamper-evident (FR-007) and short-lived (FR-006) to bound replay risk from a captured/photographed code; verification must check the scanning tablet's location against the code's issuing child's enrolled location (FR-010) before creating any attendance record; code issuance is scoped server-side to the requesting parent's own child (existing `ParentOnly` + child-ownership pattern, same as feature 031's signed-URL precedent) — a parent can never issue a code for a child they aren't linked to.

**Performance considerations**: code verification sits in the caregiver's scan-to-confirmation path and must resolve well within the 10-second SC-003 budget — no heavyweight computation beyond a signature/TTL check and the existing attendance-toggle write.

**Testing requirements**: setting-toggle tests (default-disabled for existing + new locations, save/revert-on-failure, structured log entry per FR-016); code lifecycle tests (issuance, expiry, tamper-rejection, wrong-location-rejection); parity tests proving a QR-originated `AttendanceRecord` is indistinguishable from a manually-tapped one for BKR/reporting purposes (FR-014, mirrors feature 031's RBAC-parity test pattern); offline-queue test reusing feature 008's existing reconciliation test harness.

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
