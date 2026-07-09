# Feature Specification: Daily Attendance Registration

**Feature Branch**: `010-attendance`

**Created**: 2026-07-09

**Status**: Draft

**Input**: User description: "Build daily attendance registration — the core operational record of which children are present at the KDV each day. attendance_records table (per child per location per day), caregiver tablet one-tap check-in/check-out pre-populated from contracted schedule, offline sync with server-wins conflict policy, absence registration (justified/unjustified), closure-day auto-absence hook for feature 011, live BKR (begeleider-kind-ratio) indicator sourced from feature 008a's room shift roster, planned_duration_minutes derived from contract schedule for Phase 3 Opgroeien reporting."

## Product Context

### Feature Type

Mixed (API-backend capability + User-facing UI).

### Primary Consumer

Caregiver (primary — tablet check-in/check-out + BKR indicator), Director (secondary — corrections + monitoring).

### Workflow Boundary

Primary workflow: **Attendance & Presence** (`Workflows/attendance.md`). That document currently
only sketches the high-level flow (arrival → confirm → record → status change → parent
confirmation → director reporting) with no mention of BKR, closure days, or offline sync. This
feature is the concrete implementation of that workflow and updates the document accordingly as
part of this spec (see the workflow-doc diff noted in Assumptions).

Secondary workflow touchpoint: **Classroom Operations** (`Workflows/classroom-operations.md`).
BKR's "on-duty qualified staff" figure is sourced from feature 008a's `RoomShift` roster (who is
currently checked in at a location) — the only on-duty-staff signal that exists at this point in
the backlog. Feature 012 (scheduling), not yet built, is expected to extend this BKR calculation
via `staff_schedules` later (per BACKLOG.md's own 012 entry) — this feature does not wait for
that; it uses what exists today.

Actors: Caregiver (tablet), Director (web, corrections + monitoring), System (offline sync,
device-token authenticated writes).

Data Flow: caregiver taps a child on the group view → `attendance_record` created/updated
(online: direct write; offline: queued via feature 008's offline_queue) → BKR indicator
recomputed from present-count at the location + the `RoomShift` roster → director web reads/edits
records for corrections.

Outputs: `attendance_records` rows; a live BKR indicator (caregiver tablet only — no director-web
BKR requirement is specified for this feature); corrected records.

Cross-platform Impact: caregiver tablet (primary UI), director web (correction screen — first
real UI for director-only attendance corrections, following feature 009's precedent of
device-token + director dual-auth on correction routes), backend (all of it). Parent mobile is
not touched by this feature — exchange/extra-day *requests* are feature 013's concern; this
feature only needs the ability to record an attendance day outside the contracted schedule
(already covered by the "extra day" check-in case below), not the parent-facing request/approval
flow itself.

### User Impact

This enables caregivers to record which children are physically present each day with a single
tap, resulting in an accurate, real-time attendance register that directors can rely on for
compliance and billing inputs (feature 014) without manual reconciliation.

### UX Requirements

- **Persona**: Caregiver, standing, one-handed, tablet mounted/laid flat, landscape-locked.
- **Platform**: Caregiver tablet (primary), director web (corrections + history, secondary).
- **User job**: "Mark who's here today, in one tap, without breaking my flow with the kids."
- **Success criteria**: check-in/out completes in a single tap from the group view; BKR status
  is readable at a half-second glance; absence marking takes no more than a couple of taps.
- **Main flow**: group view → tap child → present (check-in). Tap again → check-out. Absence is
  a separate, deliberate action (not the same one-tap gesture, to avoid accidentally marking a
  child absent) → justified/unjustified classification + optional reason.
- **Loading/empty/error states**: today's expected list is pre-populated from each child's
  contracted schedule and loads with the existing group view; a child with no contract entry for
  today's weekday can still be manually checked in (an "extra day"); offline banner/pending-sync
  indicator reuses feature 008's existing components — no second offline UI is built.
- **Accessibility**: 48pt minimum touch targets (64pt is not required here — this isn't the
  single highest-frequency kiosk interaction like PIN entry); the BKR color indicator is never
  the only signal — it is always paired with a text label/icon, not color alone.
- **Offline behavior**: check-in/out/absence queue via feature 008's `offline_queue`; conflict
  policy is **server-wins** with a 409-on-duplicate rule — this differs from feature 009's
  "all writes preserved" policy and is spec'd here as its own conflict rule (see FR-012/FR-013),
  not inherited by assumption.

### Technical Requirements

- **API impact**: new endpoints for check-in, check-out, absence marking, director correction,
  and a BKR-ratio read scoped to a location.
- **Data-model impact**: new `attendance_records` table (tenant schema), one row per child per
  location per day (unique constraint), FKs to `Child`/`Location`.
- **Security considerations**: caregiver tablet writes are device-token authenticated (no
  per-caregiver auth on this route family, consistent with 008a/009); director corrections use
  the existing `DirectorOnly`/`DeviceOrDirector` policies, not a new auth mechanism.
- **Performance considerations**: BKR computation must be cheap enough for a frequently-refreshed
  read — a present-count query plus the existing `RoomShift` roster query, both already scoped
  and indexed per location.
- **Testing requirements**: unique-per-child-per-day constraint under concurrent check-ins;
  offline sync conflict handling (409/server-wins) with out-of-order arrival; BKR threshold
  boundary cases (8/9/14 — the leefgroep 18-cap is out of scope, see Assumptions);
  contract-schedule-derived `planned_duration_minutes` conversion, including the
  no-matching-contract-day ("extra day") case.

## Clarifications

### Session 2026-07-09

- Q: `recorded_by` — the originating backlog literal specifies a single `UUID` column, but
  device-token-authenticated tablet actions carry no individual caregiver identity (the same
  constraint feature 009 hit for its own `recorded_by`, resolved there with a JSONB array of
  every caregiver checked in at the time). Should attendance follow that same array precedent, or
  keep a single nullable UUID and accept ambiguity when multiple caregivers are checked in? → A:
  Follow feature 009's precedent — store every caregiver checked in at the time as a JSONB array,
  for the same reason: a device-token request has no way to attribute the action to exactly one
  of possibly several checked-in caregivers.
- Q: Feature 010's own prompt references closure days (feature 011, not yet built) auto-marking
  attendance and parent-approved exchange/extra-day requests (feature 013, not yet built)
  appearing in this view — both are forward dependencies on features that don't exist. Should 010
  build placeholder mechanisms for these now? → A: No — follow the established extension-point
  pattern already used for deactivation guards (features 004/005/006): 010 ships the `closure`
  status value and the rule that a `closure`-status record blocks manual check-in, but the actual
  bulk-generation of closure records from a closure calendar is feature 011's job to add when it
  lands. Exchange/extra-day *requests* are entirely feature 013's concern (the parent-facing
  request/approval UI) — 010 already supports checking in a child on a day their contract doesn't
  cover (the "extra day" case), which is the only piece 013 will actually need to build on.
- Q: How should the system determine when the relaxed "nap time" BKR ratio (max 14) applies,
  versus the normal 8 (solo) / 9-per-caregiver ratio? → A: Infer automatically — a location is
  considered in nap time when at least half of its currently-present children have an open
  (in-progress) sleep event recorded via feature 009's child events. Fully automatic, no
  caregiver action needed.
- Q: How should the system determine a room/group is a "leefgroep" (living group, cap 18) rather
  than a standard group subject to the per-caregiver ratio math? → A: Out of scope for Phase 1 —
  no leefgroep/group-type distinction exists anywhere in the data model today. Apply only the
  ratio-based thresholds (8 solo / 9-per-caregiver, relaxed to 14 during inferred nap time)
  uniformly to every location; the 18-cap leefgroep case is deferred to a future feature once
  groups can be flagged as such.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Caregiver checks a child in and out with one tap (Priority: P1)

A caregiver opens the group view and sees today's expected children, pre-populated from their
contracted schedules. They tap a child's card to check them in; the card visually updates to a
present state with the check-in time. At pickup, they tap again to check the child out.

**Why this priority**: This is the core operational loop the whole feature exists for — every
other capability (BKR, absences, corrections) is meaningless without an accurate record of who's
actually present.

**Independent Test**: Log in as a caregiver, open the group view, check a child in, confirm the
card reflects present status with a check-in time, check them out, confirm the card reflects
checked-out status with a check-out time — independent of any other story.

**Acceptance Scenarios**:

1. **Given** a caregiver is viewing today's group, **When** they tap a child who has not yet been
   checked in, **Then** an `attendance_record` is created with `status = present` and
   `check_in_at = now`, and the card updates immediately.
2. **Given** a child is currently checked in, **When** the caregiver taps their card again,
   **Then** `check_out_at` is set to now and the card reflects checked-out status.
3. **Given** a child's contract does not cover today's weekday, **When** a caregiver checks them
   in anyway (an approved extra day), **Then** the check-in still succeeds and creates a record.
4. **Given** the tablet has no network connectivity, **When** a caregiver checks a child in or
   out, **Then** the action is queued via the existing offline mechanism and reflected
   immediately in the local UI as pending sync.

---

### User Story 2 - Caregiver sees a live BKR ratio indicator (Priority: P1)

While working the room, a caregiver glances at a colour-coded indicator showing whether the
current ratio of present children to on-duty qualified staff is within the legal limit.

**Why this priority**: BKR compliance is a legal requirement for Belgian KDVs and the reason this
feature exists ahead of other operational features — a caregiver needs to know at a glance
whether the room is within ratio, without doing the arithmetic themselves.

**Independent Test**: With a known number of present children and checked-in qualified staff at a
location, request the BKR indicator and confirm it reflects the correct colour/status for that
combination — independent of check-in/out UI specifics.

**Acceptance Scenarios**:

1. **Given** 6 children are present and 1 qualified caregiver is checked in (solo, max 8),
   **When** the BKR indicator is computed, **Then** it shows green (within ratio).
2. **Given** 9 children are present and 1 qualified caregiver is checked in (solo, max 8),
   **When** the BKR indicator is computed, **Then** it shows red (breached) but does not block
   check-in.
3. **Given** 2 qualified caregivers are checked in (max 9 per caregiver = 18) and 17 children are
   present, **When** the BKR indicator is computed, **Then** it shows within-ratio status.
4. **Given** a student/volunteer is checked in alongside one qualified caregiver, **When** the BKR
   indicator is computed, **Then** the student/volunteer does not count toward the staff
   denominator.
5. **Given** zero qualified staff are checked in and at least one child is present, **When** the
   BKR indicator is computed, **Then** it shows red/breached rather than dividing by zero or
   hiding the indicator.
6. **Given** at least half of the present children have an open (in-progress) sleep event and 1
   qualified caregiver is checked in, **When** the BKR indicator is computed, **Then** the nap-time
   threshold (max 14) applies instead of the normal solo threshold (max 8).
7. **Given** fewer than half of the present children have an open sleep event, **When** the BKR
   indicator is computed, **Then** the normal (non-nap) threshold applies.

---

### User Story 3 - Caregiver or director marks a child absent with a justification (Priority: P2)

A caregiver or director marks a child absent for the day and records whether the absence was
pre-approved (justified) or not (unjustified), with an optional free-text reason.

**Why this priority**: Absences need to be captured accurately for compliance and later billing
(feature 014) but this is not the moment-to-moment operational loop that check-in/out and BKR are.

**Independent Test**: Mark a child absent as justified with a reason, then as unjustified,
confirming each classification is stored correctly and the child does not appear as present in
today's count — independent of check-in/BKR stories.

**Acceptance Scenarios**:

1. **Given** a child has no attendance record yet today, **When** a caregiver marks them absent
   and justified with a reason ("sick, doctor's note"), **Then** a record is created with
   `status = absent`, `absence_justified = true`, and the reason stored.
2. **Given** a child has no attendance record yet today, **When** a director marks them absent
   and unjustified, **Then** a record is created with `absence_justified = false`.
3. **Given** a child is marked absent, **When** the day's present-count or BKR ratio is computed,
   **Then** that child is excluded from the present count.

---

### User Story 4 - Director corrects a missed check-out or wrong entry (Priority: P2)

A caregiver forgets to check a child out at end of day. The following morning, a director opens
the attendance record and corrects it — setting the missing check-out time or fixing an
incorrectly recorded status.

**Why this priority**: Missed check-outs are a routine, expected occurrence (an entire day's
operational reality), not an edge case — without a correction path this data would be
permanently wrong.

**Independent Test**: Create an attendance record with a check-in but no check-out, then have a
director correct it with a check-out time, confirming the update succeeds regardless of which day
the original record is from — independent of other stories.

**Acceptance Scenarios**:

1. **Given** an attendance record has a check-in but no check-out from a prior day, **When** a
   director sets a check-out time, **Then** the record updates successfully.
2. **Given** an attendance record was created with the wrong status (e.g., marked absent by
   mistake), **When** a director corrects the status, **Then** the change is saved and reflected
   in any subsequent read.
3. **Given** a caregiver (not a director) attempts to correct a record from a prior day, **When**
   they submit the change via the tablet, **Then** the system rejects it (same-day-only for
   caregiver corrections, any-day for directors — consistent with feature 009's precedent).

---

### Edge Cases

- A child is checked in twice in quick succession from two different tablets (race condition) —
  the unique per-child-per-day constraint must resolve this without creating duplicate records or
  silently discarding one of the two events' intent (see FR-012/FR-013 for the exact conflict
  rule).
- A tablet goes offline for several hours with multiple check-ins/check-outs queued for the same
  children; on reconnect, events may arrive out of order relative to when they occurred — the
  server's own record timestamp is authoritative, not the client's queue order.
- A child is checked in on a day their contract doesn't cover (extra day) — `planned_duration_minutes`
  has no contracted-schedule value to derive from (see FR-006a).
- A record already has `status = closure` (set by a future feature 011 mechanism) — manual
  check-in against that record must be rejected.
- Zero qualified staff are checked in at a location while children are present — BKR must not
  divide by zero or fail to render.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow creating an attendance record for a specific child at a specific
  location and date, via a check-in action that sets `status = present` and `check_in_at = now`.
- **FR-001a**: A check-in request against an existing `status = absent` record for the same
  child/location/date MUST transition that record to `status = present` (setting `check_in_at`)
  rather than being rejected as a duplicate — a caregiver marking a child present after an earlier
  mistaken/premature absence mark is a legitimate correction, not a conflict. A check-in against an
  existing `status = present` record remains a duplicate (FR-012).
- **FR-002**: System MUST allow setting `check_out_at` on an existing present-status record for
  the same child/location/date.
- **FR-002a**: A check-out request against a record with no `check_in_at` set, or one that already
  has `check_out_at` set, MUST be rejected as not-found (`404`) rather than silently overwriting an
  existing check-out time or checking out a child who was never checked in.
- **FR-003**: System MUST enforce at most one `attendance_record` per child per location per day
  (unique constraint) — a second check-in attempt for the same child/location/date is a
  duplicate, not a new record (see FR-012).
- **FR-004**: System MUST allow checking in a child whose contract does not cover today's weekday
  (an approved "extra day") — the record is created the same as any other check-in.
- **FR-005**: System MUST allow marking a child absent for the day, recording `absence_justified`
  (boolean) and an optional free-text `absence_reason`, creatable by either a caregiver (via
  device token) or a director. An absence-mark request racing a check-in request for the same
  child/location/date is resolved by the same unique constraint and 409/conflict rule as two
  racing check-ins (FR-003/FR-012) — whichever write reaches the database first wins; the loser
  receives the same 409 response.
- **FR-006**: System MUST derive `planned_duration_minutes` from the child's active contract at
  that location for that weekday, converting the contracted start/end time into minutes (e.g., an
  8-hour contracted Wednesday = 480 minutes). When a child holds two simultaneous contracts at two
  different locations of the same tenant (feature 007's split-location case), the derivation MUST
  use only the contract matching the attendance record's own `location_id`, never any of the
  child's other contracts.
- **FR-006a**: When no contracted-day entry exists for the child at that location on that weekday
  (an extra day, FR-004), `planned_duration_minutes` MUST be stored as null rather than a
  fabricated or zero value — a future feature may derive an actual-duration fallback from
  `check_in_at`/`check_out_at` if needed, but this feature does not compute one.
- **FR-007**: System MUST compute a live BKR ratio for a given location: present children present
  right now ÷ qualified on-duty staff checked in right now (via the existing `RoomShift` roster),
  applying the fixed thresholds: solo caregiver max 8, 2+ caregivers max 9 per caregiver (i.e. cap
  = 9 × qualified caregivers checked in), relaxed to max 14 per caregiver during inferred nap time
  (FR-007c). The living-group (leefgroep) 18-cap regime is out of scope for this feature (see
  Assumptions) since no group is flagged as a leefgroep anywhere in the data model yet.
- **FR-007a**: Staff whose `QualificationLevel` is `StudentVolunteer` MUST NOT count toward the
  qualified-staff denominator in the BKR calculation, even if checked in.
- **FR-007b**: When zero qualified staff are checked in while at least one child is present, the
  BKR indicator MUST show a breached/red state, not an error or a hidden indicator. A location
  with no `RoomShift` history at all (never set up) and a location with a `RoomShift` history but
  nobody currently checked in are indistinguishable from the BKR calculation's point of view —
  both yield zero qualified staff and are treated identically (breached if any child is present,
  else `green`/no-breach-possible with zero present and zero staff).
- **FR-007c**: A location MUST be considered in "nap time" for BKR purposes when the count of its
  currently-present children with an open (in-progress, no `ended_at`) sleep event, multiplied by
  2, is greater than or equal to its total currently-present-children count (i.e., "at least half,"
  rounding up for an odd present-count — 2 napping out of 3 present qualifies, 1 out of 3 does
  not) — this determination is automatic and requires no caregiver action.
- **FR-007d**: A child with `status = absent` or `status = closure` MUST be excluded from the BKR
  present-count (FR-007) — only `status = present` records with no `check_out_at` set count as
  currently present.
- **FR-007e**: The BKR indicator's threshold is a three-way state: `green` when the present count
  is strictly below the applicable threshold, `amber` when the present count exactly equals the
  applicable threshold (at capacity, not yet breached), `red` when the present count exceeds the
  applicable threshold (breached) or when zero qualified staff are checked in with at least one
  child present (FR-007b).
- **FR-008**: The BKR indicator MUST be presented with a colour (green/amber/red) that is never
  the only signal — it is always paired with a text label or icon distinguishable without colour.
- **FR-008a**: The BKR indicator MUST reflect a check-in, check-out, or absence-mark action taken
  on the same device within 5 seconds of that action (immediate local recomputation, not dependent
  on a server round-trip) and MUST otherwise refresh from the server at least every 15 seconds so
  a change made by staff on a different device/tablet in the same room becomes visible promptly.
- **FR-009**: System MUST NOT hard-block a check-in when it would breach the BKR ratio in this
  phase — the ratio is warning/display-only.
- **FR-010**: System MUST allow a caregiver to correct (edit or delete) a same-day attendance
  record — "same day" meaning the record's `date` equals today's `Europe/Brussels`-anchored
  calendar day, the identical boundary feature 009's FR-006/FR-018a already established — from any
  tablet paired to the record's own location, following the same device-authenticated,
  location-scoped rule established by feature 009's FR-006 (no individual caregiver identity is
  available on this route family).
- **FR-011**: System MUST allow a director to correct (edit or delete) any attendance record
  regardless of when it was recorded.
- **FR-011a**: A correction (FR-010/FR-011) MUST enforce the same status/field invariants as
  creation: a record corrected to `status = present` MUST have `check_in_at` set; a record
  corrected to `status = absent` MUST have `absence_justified` set (non-null) and MUST NOT retain
  a `check_in_at`/`check_out_at` value from a prior state. A correction that would leave the
  record in an invalid combination (e.g., `present` with no `check_in_at`) MUST be rejected the
  same way an invalid creation request is (FluentValidation pipeline, per constitution Principle
  III), not silently accepted.
- **FR-012**: A duplicate check-in request for a child/location/date that already has a record
  MUST be rejected with a 409 response; the client marks the queued offline action as synced with
  a conflict note rather than retrying it.
- **FR-013**: When an offline-queued check-in/check-out/absence action reaches the server after a
  delay, the server's own record state at time of processing is authoritative (server-wins) — a
  stale client-side view of "not yet checked in" that conflicts with server state results in the
  409/conflict-note behavior of FR-012, not an overwrite of the server's data.
- **FR-014**: `recorded_by` MUST capture every caregiver checked in (via the location's
  `RoomShift` roster) at the moment of the action, stored as a set of staff identifiers — mirrors
  feature 009's `recorded_by` precedent for the same reason: a device-token action has no
  single individual identity to attribute to.
- **FR-015**: The `attendance_records.status` enum MUST include a `closure` value; a record
  already at `status = closure` MUST reject any manual check-in attempt against it. Bulk-creation
  of `closure`-status records from a closure calendar is out of scope for this feature (feature
  011's responsibility once built).
- **FR-016**: All user-facing strings for this feature MUST be presented via the existing i18n
  mechanism (NL/FR/EN), with no hardcoded user-facing strings.
- **FR-017**: The caregiver-tablet check-in/check-out action MUST require exactly one tap from
  the group view (no intermediate confirmation screen); the absence-marking action MUST be a
  visually and interactionally distinct action from check-in/out, to prevent an accidental
  one-tap absence, and MUST require no more than 3 taps/selections: (1) opening the distinct
  absence action for a child, (2) selecting justified or unjustified, (3) confirming — entering an
  `absence_reason` is optional free text and, like `activity`/`note` in feature 009's FR-021, is
  exempt from the tap-count limit.

### Key Entities

- **Attendance Record**: One per child per location per day — tracks presence status
  (present/absent/closure), check-in/check-out times, the contract-derived planned duration for
  that day, absence justification and reason (when absent), and who was checked in at the time
  of the recording action.
- **BKR Ratio** (computed, not stored): For a given location at the moment of the read — count of
  currently-present children, count of currently-checked-in qualified staff, and the resulting
  status against the fixed threshold table.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A caregiver can check a child in or out in a single tap from the group view, with
  the UI reflecting the new state immediately (before server confirmation, when offline).
- **SC-002**: 100% of check-in/out/absence actions recorded while offline are correctly reflected
  once connectivity returns, with no duplicate attendance records created for the same
  child/location/day.
- **SC-003**: The BKR indicator correctly reflects status (green/amber/red) at each of the
  following boundary combinations, in 100% of tested cases: solo qualified caregiver at 7/8/9
  present children (non-nap); solo qualified caregiver at 13/14/15 present children (nap-time
  inferred); 2 qualified caregivers at 17/18/19 present children (non-nap); 2 qualified caregivers
  at 27/28/29 present children (nap-time inferred); zero qualified caregivers with ≥1 child
  present. (The leefgroep 18-cap regime is out of scope per Assumptions — not included here.)
- **SC-004**: A director can correct any historical attendance record (missed check-out, wrong
  status) without developer or database intervention.
- **SC-005**: Every attendance record's `planned_duration_minutes` value is verifiable by direct
  comparison against the child's `Contract.ContractedDays` entry for that location/weekday (the
  exact minutes computed from that entry's start/end time), or is `null` when no such entry exists
  — never a value that doesn't trace back to one of those two cases.
- **SC-006**: The BKR indicator reflects a locally-taken check-in/check-out/absence action within
  5 seconds, and reflects an action taken on a different device in the same room within 15 seconds
  (FR-008a).

## Assumptions

- **`recorded_by` deviates from the originating backlog's literal single-`UUID` column** to a
  JSONB array of staff identifiers, mirroring feature 009's resolved precedent for the same
  underlying constraint (device-token routes carry no individual caregiver identity — only the
  `RoomShift` roster tells you who was checked in at the time).
- **Closure-day bulk-marking (feature 011) and parent-approved exchange/extra-day requests
  (feature 013) are explicitly out of scope** for this feature, beyond the `closure` status value
  and the pre-existing "extra day" check-in capability — both are forward dependencies on
  features not yet built, resolved the same way prior features handled forward dependencies
  (e.g., `ILocationDeactivationGuard`/`IStaffDeactivationGuard` extension points registered with
  zero implementations until the dependent feature lands).
- BKR's "on-duty qualified staff" is sourced entirely from feature 008a's `RoomShift` roster
  (who is currently checked in) for this feature — feature 012 (staff scheduling) is expected to
  extend or refine this later, per BACKLOG.md's own note on that feature. **Accepted limitation**:
  a caregiver who is physically present but forgot to check in via 008a's PIN flow does not count
  toward the qualified-staff denominator — the roster is the only on-duty signal this feature has
  access to, and this is the same limitation feature 008a's own shift-register model already
  carries for attribution purposes generally, not a new gap introduced here.
- The same-day/any-day correction authorization split (FR-010/FR-011) reuses feature 009's
  resolved device-location-match pattern rather than a per-caregiver eligibility check, for the
  same reason 009 arrived at that design: device-token routes carry no individual caregiver
  identity to check eligibility against.
- `planned_duration_minutes` is derived once at check-in time from the child's active contract at
  that location for that weekday; it is not recomputed retroactively if the contract changes
  after the attendance record already exists.
- The BKR ratio is computed at read time (not stored) — consistent with feature 009's daily
  summary being computed at query time rather than materialized.
- **The leefgroep 18-cap BKR regime is out of scope for this feature** — no `Group`/`Location`
  field distinguishes a leefgroep from a standard room today. Every location, without exception,
  is evaluated under the same ratio-based thresholds (8 solo / 9-per-caregiver / 14 during
  inferred nap time) implemented by this feature — no location is silently exempted from BKR
  computation entirely, and none falls through an undefined gap between "ratio-based" and
  "leefgroep-based" enforcement; there is simply only one enforcement regime today. A future
  feature can add a group-type flag and branch the calculation for leefgroep locations once that
  distinction exists.
- **"Nap time" for BKR purposes is inferred, not manually toggled or scheduled** — at least half
  of a location's currently-present children having an open sleep event (feature 009) triggers
  the relaxed threshold automatically.
- A child may have at most one active contract per location (per feature 007's split-location
  rule) — `planned_duration_minutes` derivation looks up the single active contract for that
  child at that specific location, not across all of the child's contracts tenant-wide.
