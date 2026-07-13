# Feature Specification: Configurable Caregiver PIN

**Feature Branch**: `008b-configurable-caregiver-pin`

**Created**: 2026-07-13

**Status**: Draft

**Input**: User description: "Add a per-location director-web setting to make the caregiver PIN
step optional at check-in/check-out, for KDVs whose way of working doesn't fit a per-caregiver
PIN entry — while keeping the underlying tap-to-identify check-in flow intact, since BKR ratio
(010) and event/incident attribution (009/010/013b) both depend on RoomShifts existing, not on
the PIN itself."

## Clarifications

### Session 2026-07-13

- Q: Does the medical/sensitive-action confirmation step (used when logging medication or
  temperature events) also skip PIN entry when the location's PIN requirement is off, or does it
  always require PIN verification regardless of the location setting? → A: Always require PIN
  verification for this step, regardless of the location's PIN-requirement setting. It exists
  specifically as an extra verification step for medical/sensitive actions — a higher bar than
  routine check-in/check-out presence — and already has its own escape hatch (the existing
  "Skip" action, which records no administering caregiver and lets the director fill it in
  retroactively). Locations that want to bypass this step already can via that existing
  mechanism, so this feature's setting only ever governs routine check-in/check-out.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director disables PIN verification for a location (Priority: P1)

A director whose KDV's way of working doesn't fit a per-caregiver PIN step opens that location's
settings in the web admin and turns off "PIN vereist bij in-/uitchecken". Explanatory copy makes
clear what this trades away: check-in still requires a caregiver to identify themselves by
tapping their own card, but that claim is no longer verified by a PIN — it becomes self-asserted.
Legal ratio tracking and event/incident attribution both keep working exactly as before, since
both already read only from the shift record itself, not from whether a PIN was checked.

**Why this priority**: This is the entire point of the feature — without it, every KDV is forced
into PIN verification regardless of fit, which is the problem a director already raised.

**Independent Test**: Can be fully tested by a director toggling the setting off for one location,
saving, and verifying only that location's behavior changes — a second location is unaffected.

**Acceptance Scenarios**:

1. **Given** a director is viewing a location's settings, **When** they open the relevant tab,
   **Then** they see the current state of the PIN-requirement setting (defaulting to "required"
   for any location that has never changed it) alongside copy explaining the tradeoff of turning
   it off.
2. **Given** the director turns the setting off and saves, **When** the save completes, **Then**
   the location's PIN requirement is persisted as off and every other location's setting is
   unaffected.
3. **Given** the setting is off, **When** the director turns it back on and saves, **Then**
   check-in/check-out at that location immediately requires PIN verification again for the next
   action (any already-open shifts are unaffected — see Edge Cases).

---

### User Story 2 - Caregiver checks in/out with no PIN step (Priority: P1)

At a location where the director has turned the setting off, a caregiver on the room tablet taps
their own photo card to check in (or out). The action completes immediately — no PIN keypad
appears. The room's "who's here" list updates the same way it always has.

**Why this priority**: This is the actual day-to-day experience the setting exists to produce;
without it, the director-facing toggle in User Story 1 has no observable effect for the people
it's meant to help.

**Independent Test**: Can be fully tested by a caregiver tapping their card at a PIN-off location
and observing check-in completes with no keypad shown, versus the existing PIN-on behavior at an
unaffected location.

**Acceptance Scenarios**:

1. **Given** a location has the PIN requirement off, **When** a caregiver taps their own
   unchecked-in card, **Then** they are checked in immediately with no PIN prompt, and their card
   shows the checked-in state.
2. **Given** the same location and a caregiver who is already checked in, **When** they tap their
   card again, **Then** they are checked out immediately with no PIN prompt.
3. **Given** a location has the PIN requirement on (the default), **When** a caregiver taps their
   card, **Then** the existing PIN keypad step is required exactly as before — this feature
   changes nothing about that location's experience.

---

### User Story 3 - BKR ratio and event attribution are unaffected (Priority: P1)

A director or an auditor reviewing legally-required occupancy-ratio reporting, or checking who
recorded/administered a given child event or incident, sees no difference in accuracy or
completeness whether or not a location has PIN verification turned on. The underlying shift
record that both calculations read from is written identically either way.

**Why this priority**: This is the constraint that makes the feature safe to ship at all — a
regression here would be a legal-compliance and audit-trail defect, not just a UX gap.

**Independent Test**: Can be fully tested by comparing ratio and attribution calculations for two
otherwise-identical shifts, one created with PIN verification on and one with it off, and
confirming the calculations treat them identically.

**Acceptance Scenarios**:

1. **Given** two caregivers are checked in at a PIN-off location, **When** the current staffing
   ratio is calculated, **Then** both caregivers count exactly as they would if PIN had been
   required.
2. **Given** a caregiver checked in without PIN verification records a routine event, **When** the
   event's recorded-by information is resolved, **Then** it resolves to that caregiver exactly as
   it would have under PIN verification.

---

### Edge Cases

- A location has the PIN requirement off, then a director turns it back on mid-day while
  caregivers are already checked in. Existing open shifts from before the change are unaffected —
  they were validly created under the rules in force at the time. The PIN step reappears only for
  the next check-in/check-out action at that location.
- Two caregivers have visually similar photos/names and the PIN requirement is off — a caregiver
  taps the wrong card by mistake. There is no PIN to catch this. The only correction path is
  noticing and checking the wrong person out / the right person in. This risk must be named
  explicitly in the director-facing tradeoff copy, not left implicit.
- The existing "skip PIN confirmation while offline" behavior for the medical/sensitive-action
  confirmation step is a distinct mechanism from this always-off-by-setting behavior. The two
  must remain visibly distinct in any audit trail or record of which path produced a given
  attribution — never collapsed into one unlabeled "no PIN" case.
- A location's PIN requirement is off, and a caregiver who has never set a PIN (or whose PIN was
  reset and never replaced) is checked in/out anyway — this must work, since no PIN is verified.
- Existing caregiver PINs are never deleted or invalidated by turning the requirement off.
  Turning it back on later must not force every caregiver to set a new PIN.
- A room tablet that was offline when a director changed the setting continues operating on the
  last setting value it fetched, exactly like every other per-location fact the tablet caches
  (per feature 008's offline read-cache model) — the new value takes effect on the tablet's next
  successful roster fetch, not instantly.
- This setting is a single whole-location on/off switch — it MUST NOT be interpreted as, or
  implemented as, a per-caregiver override. Every caregiver at a given location is governed by
  the same value at any given moment.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Directors MUST be able to view and change, per location, whether caregiver PIN
  verification is required at check-in/check-out.
- **FR-002**: The PIN-requirement setting MUST default to "required" for any location that has
  never explicitly changed it, so no existing location's behavior changes without a deliberate
  director action.
- **FR-003**: The director-facing setting screen MUST present explanatory copy, in plain
  operational language a non-technical director can act on (not internal terms like "self-asserted
  identity"), stating: (a) a caregiver's identity at check-in/out will no longer be confirmed by
  anything beyond their own card tap, and (b) the concrete consequence — if a caregiver taps the
  wrong card by mistake, nothing catches it; the only fix is someone noticing and correcting the
  check-in/out afterward. This copy MUST be shown before/alongside the toggle, not hidden behind a
  secondary disclosure.
- **FR-004**: When a location's PIN requirement is off, checking in or out MUST NOT prompt for or
  verify a PIN — the tap-to-identify action alone MUST complete the check-in/check-out.
- **FR-005**: When a location's PIN requirement is off, the check-in/check-out action MUST still
  produce the same underlying shift record (identifying which caregiver, which location, which
  timestamp) as it would if PIN verification had occurred, so that every downstream reader of
  that record behaves identically either way.
- **FR-006**: When a location's PIN requirement is on (the default), check-in/check-out MUST
  continue to require PIN verification exactly as it does today — this feature introduces no
  change to that path.
- **FR-007**: The system MUST determine whether PIN verification applies for a given check-in/
  check-out attempt by checking the current per-location setting at the time of the attempt, not
  by trusting a client-supplied flag — the server, not the tablet, is the enforcement point.
- **FR-008**: The caregiver-tablet check-in/check-out screen MUST NOT show a PIN entry step when
  the location's setting is off, and MUST show it when the setting is on — the client's own
  presentation of the step MUST reflect the current location setting.
- **FR-009**: Turning the PIN requirement off or on MUST NOT delete, reset, or invalidate any
  caregiver's existing PIN. Turning the requirement back on MUST NOT require caregivers to set a
  new PIN before they can check in/out again.
- **FR-010**: Shift records created while the PIN requirement was off MUST remain valid and
  untouched if the director later turns the requirement back on — only subsequent actions are
  affected, never past ones.
- **FR-011**: The legally-required staffing-ratio calculation MUST count caregivers identically
  regardless of whether their current shift was created with or without PIN verification.
- **FR-012**: Event and incident recorded-by/administered-by attribution MUST resolve identically
  regardless of whether the relevant caregiver's current shift was created with or without PIN
  verification.
- **FR-013**: The medical/sensitive-action confirmation step (used when logging medication or
  temperature events) MUST continue to require PIN verification regardless of a location's
  PIN-requirement setting — this feature's setting governs routine check-in/check-out only, and
  does not extend to this separate, higher-bar confirmation step. Its existing "Skip" path
  (recording no administering caregiver) remains the only way to bypass it, unchanged by this
  feature.
- **FR-014**: All new user-facing text introduced by this feature (the toggle label and the
  tradeoff explanatory copy) MUST be available in Dutch, French, and English via the existing
  i18n mechanism — no hardcoded strings.
- **FR-015**: If saving the setting fails (network error, validation error, or the director is no
  longer authorized), the director MUST see a clear, human-readable error and the toggle MUST
  revert to its last-saved state — never silently appear changed when the save did not succeed.
- **FR-016**: Every change to this setting MUST produce a structured server-side log entry
  recording which director changed it, which location, the old and new value, and when — since
  this setting deliberately trades away an identity-assurance guarantee, the decision to do so
  must itself be traceable, even though this codebase has no dedicated queryable audit-trail
  mechanism today (this is a plain application log entry, not a new audit subsystem).
- **FR-017**: This setting is a single value per location — it MUST NOT vary by caregiver. The
  same value governs every caregiver's check-in/check-out at that location at any given moment
  (see Assumptions).
- **FR-018**: This setting's current value is an internal operational configuration, not a
  compliance artifact — it MUST be viewable by a director like any other location setting, but
  this feature introduces no new external-facing (e.g. Opgroeien/inspector) report or field for
  it. Should a future regulatory reporting feature need this value, that is that feature's
  decision to make, not an implicit requirement of this one.

### Key Entities

- **Location** (existing entity, extended): gains a single setting recording whether caregiver
  PIN verification is required at check-in/check-out for that location. Defaults to required.
- **Room Shift** (existing entity, unchanged): the record of a caregiver's presence in a room
  (check-in time, check-out time, which caregiver, which location). This feature does not add
  any field to this record — the same record is produced whether or not PIN verification
  occurred, which is the mechanism that keeps ratio/attribution unaffected.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can change a location's PIN requirement and see the change reflected
  in explanatory copy and the toggle state within the same settings visit, with no page reload
  needed to confirm the save succeeded.
- **SC-002**: At a location with the PIN requirement off, a caregiver completes check-in in a
  single tap, with zero additional steps compared to today's tap-then-PIN flow.
- **SC-003**: 100% of shift records — regardless of whether PIN verification occurred — produce
  identical staffing-ratio and event/incident-attribution results in side-by-side comparison
  testing.
- **SC-004**: Zero existing locations experience a behavior change immediately after this feature
  ships (the default preserves current PIN-required behavior everywhere until a director
  explicitly opts out).
- **SC-005**: 100% of the tradeoff copy shown alongside the toggle names the specific wrong-card
  risk in plain language — verifiable by a reviewer reading the copy without needing to consult
  this spec, per FR-003's two required points.

## Assumptions

- The setting is a simple per-location on/off switch, not a graduated policy (e.g. "PIN required
  only for medication events") — anything more granular is out of scope, per the feature's own
  "Out of scope" framing (this only adds the off switch, not a new PIN-lifecycle mechanism).
- "Tap-to-identify" (a caregiver selecting their own card) remains mandatory in all cases — this
  feature never introduces an anonymous or no-selection check-in path.
- The per-location setting is surfaced to the caregiver tablet via the existing roster fetch
  (rather than a separate network call), consistent with how the tablet already learns
  per-location facts today.
- Directors are the only role able to change this setting, consistent with every other
  per-location setting in this system (mirrors feature 013f's precedent).
- No change is required to how PINs are set, reset, or rate-limited when the requirement is on —
  this feature only adds the off switch.
