# Feature Specification: Staff App (Personal Rota & Leave)

**Feature Branch**: `027-staff-app`

**Created**: 2026-07-22

**Status**: Draft

**Input**: User description: "A separate Expo mobile app for caregivers/staff (distinct from the
shared caregiver group tablet). Staff use this on their personal phones to see their personal
assignment schedule, submit leave/sick requests, and receive push notifications about schedule
changes. The director manages assignments (and on-the-fly sick cover) from the web admin."

## Product Context

### Feature Type

Mixed — a new director-web "Rooster" surface (extends feature 012's existing scheduling grid
with publish/draft, contracted-days/closure greying, and on-the-fly sick-cover assignment) plus
a brand-new personal Expo mobile app for staff (schedule view, sick report, leave requests,
notifications).

### Primary Consumer

Caregiver/Staff (views own schedule, reports sick, submits leave requests); Director (builds and
publishes the rota, handles sick cover, approves/rejects leave); System (computes eligible-cover
suggestions, sends push notifications on publish/change/decision).

### Workflow Boundary

Extends the **Classroom Operations** workflow's existing "weekly staff rota" flow
(`Workflows/classroom-operations.md`, feature 012), which already documents this exact feature as
its own deferred follow-up: *"A caregiver's own schedule is readable via a personal-account-scoped
API, but has no caregiver-facing UI yet ... that UI is feature 027's job (Staff App)."* This
feature is that follow-up. It also introduces a **new sub-flow, leave requests**, which has no
existing home in `Workflows/classroom-operations.md` — added to that file as part of this spec
per `workflows.md`'s governance rules (an addition to the existing rota flow, not a new
workflow — leave changes rota status the same way absence-marking already does, just
staff-initiated instead of director-initiated).

- **Actors**: Staff/Caregiver (personal, non-shared session — distinct from the shared kiosk
  tablet's PIN-identified presence log), Director, System.
- **Actions**: Director builds/publishes a weekly rota; staff views their own published
  schedule; staff reports a sick day or submits a planned leave request; director assigns
  on-the-fly cover for a sick day; director approves/rejects a leave request.
- **Data Flow**: Director web writes rota entries (`staff_schedules`, extended) → publish flips
  visibility → staff app reads via the existing personal-account-scoped `GET
  /api/staff-schedules/me` (extended to respect publish state) → staff-initiated leave requests
  (`staff_leave_requests`, new) flow back to a director-web approval queue → approved/rejected
  decisions and rota changes push-notify the affected staff member(s).
- **Outputs**: A published personal schedule visible only to its own staff member; a
  director-side "Verlofaanvragen" (leave requests) queue; push notifications on publish,
  on-the-fly change, and leave decision.
- **Cross-platform Impact**: Director web (rota grid extensions, leave queue) and a **new**
  Expo mobile project, `staff-mobile` (alongside the existing `mobile` caregiver-tablet app and
  `parent-mobile` app — a staff member's *personal* phone, distinct from both). Backend-only
  additions support both. The shared caregiver kiosk tablet (`mobile`) is explicitly **not**
  involved, per `Workflows/classroom-operations.md`'s own stated design principle that a personal
  schedule view needs a personal session the shared tablet structurally doesn't have.

### User Impact

This enables a staff member to check their own upcoming work schedule and report absence from
their personal phone — resulting in fewer "what room am I in Wednesday?" phone calls to the
director and faster, trackable sick-cover response instead of an ad hoc phone tree.

### UX Requirements

**Persona**: A part-time or full-time KDV caregiver, checking their phone outside work hours —
not multitasking with a child in the room (that's the shared tablet's job). Comfortable with a
typical consumer app; not administratively trained.

**Platform**: `staff-mobile`, a new Expo (React Native) project, phone/portrait, personal device
— same category as `parent-mobile` (personal phone, standard mobile interaction), not the
caregiver tablet's landscape/kiosk/shared-device model.

**User job**: "Where am I working next Wednesday?" and "I need to call in sick, right now,
without phoning anyone."

**Success criteria**: A staff member can find their next working day and location in under 5
seconds of opening the app (no more than one tap past login/home); reporting sick for today takes
one tap plus a confirmation, not a form.

**Main flow**:

1. Staff logs in with personal email/password (existing `role=staff` JWT path).
2. Home screen shows the next 4 weeks of the staff member's own **published** schedule, defaulting
   to a week view with a day-view toggle; each entry shows location, group/room, start/end time.
   Days with no assignment show as free; days on a location closure show "KDV gesloten"; days
   outside the staff member's contracted days are visually de-emphasized the same way the web
   rota grid greys them, for the same reason (this is a read-only mirror of what the director
   already can't schedule them for).
3. A single "Ik ben ziek" action reports today (or tomorrow, if tapped after a cutoff — see
   Assumptions) as sick with one tap and a confirmation step; no form.
4. A separate "Verlof aanvragen" (request leave) flow lets a staff member pick a date range, a
   type (annual/other), and an optional note, then submits — this one is a short form, since it's
   planned in advance rather than urgent.
5. A simple notifications list shows schedule-published, schedule-changed, and
   leave-request-decided events, newest first.

**Loading/empty/error states**: Standard skeleton/spinner while fetching; an explicit empty state
("Geen diensten gepland" + icon, per `design-system.md`'s empty-state pattern) for a week with no
published entries — this is expected (a not-yet-published week), not an error; a network-error
state distinct from "no schedule" so a staff member never mistakes "can't reach the server" for
"I have no shifts."

**Accessibility**: 48pt minimum touch targets (mobile, per `platform-rules.md`); the "Ik ben
ziek" action needs an explicit confirmation step precisely because of its urgency and
irreversibility-by-a-normal-user (a mis-tap shouldn't silently report a healthy staff member
sick) — this is a deliberate exception to the tablet-only "prefer selection over typing, one tap"
speed principle, which applies to the caregiver *tablet*, not this personal, lower-frequency app.

**Offline behavior**: Read-only schedule view may be cached for the (already-fetched) current 4
weeks, consistent with `parent-mobile`'s existing cache-fallback pattern (013c) — matches this
app's low write-frequency, reassurance-oriented use. Sick-report and leave-request submission
require connectivity (both are director-facing, time-sensitive actions with no safe offline-queue
semantics — unlike the caregiver tablet's offline attendance queue, a sick report silently queued
and only delivered hours later when connectivity returns would defeat its own purpose).

### Technical Requirements

**API impact**:

- Extends `StaffSchedule`/`staff_schedules` (feature 012) in place — see Key Entities — rather
  than introducing a parallel table, per this feature's own corrected premise (the original
  BACKLOG prompt's `staff_assignments` sketch used the actual `staff_schedules` table's shape
  incorrectly; consolidating avoids two parallel representations of the same "who works where
  when" fact).
- Extends `GET /api/staff-schedules/me` (already built by feature 012, currently unconsumed) to
  filter to `IsPublished` rows only.
- New endpoints for: publish-a-week, mark-absent-with-cover-suggestion, assign-cover, leave
  request CRUD (staff-submitted) and decide (director-approved/rejected).
- New `StaffProfile.ContractedDays` field (feature 005 entity extension) — does not exist today;
  required for both the web grid's and the staff app's day-greying.

**Data-model impact**: One extended entity (`StaffSchedule` — see Key Entities), one new entity
(`StaffLeaveRequest`), one extended entity (`StaffProfile.ContractedDays`). All tenant-schema,
per Constitution Principle I.

**Security considerations**: A staff member must only ever read their **own** schedule and leave
requests — enforced server-side by resolving `StaffProfileId` from the JWT `NameIdentifier`
claim (existing `GetMyScheduleQuery` pattern), never a client-supplied staff ID. Assignment
create/update/cover-assignment continues to enforce `StaffLocationEligibility` (existing
mechanism from feature 012 — a staff member cannot be assigned, nor offered as cover, at a
location they aren't eligible for). Multi-tenant isolation via the existing `TenantDbContext`
path (Principle I) — no new cross-tenant surface, including for the new `StaffLeaveRequest`
entity. A leave request's free-text `Notes` field may contain employee health information (a
`sick`-typed request's reason) — a GDPR special category of personal data. `Notes` is visible
only to the submitting staff member (their own record, per FR-012) and to directors (who already
handle equivalent sensitive data — medical notes on minors — under the same tenant's existing
data-handling posture); it is never included in a push notification body (FR-008a) and carries
no retention period distinct from the `StaffLeaveRequest` row itself (no separate purge job —
out of scope for this feature, consistent with feature 038 being the future home for data-
retention lifecycle policy generally).

**Performance considerations**: Schedule reads are a small, date-bounded (4-week) query per
staff member — no new performance-sensitive path. Push notifications reuse the existing
fire-and-forget `IExpoPushSender` pattern.

**Testing requirements**: Happy path (staff sees a published schedule; sick report creates a
leave request and notifies the director; director assigns cover and both parties are notified;
leave approval flips the affected days to absent) plus the key negative/regulatory flows: a
staff member cannot read another staff member's schedule or leave requests; an unpublished week
is invisible to `GET /api/staff-schedules/me`; `StaffLocationEligibility` still blocks an
ineligible cover assignment; the existing BKR-decoupling regression test class (feature
012's `BkrDecouplingTests`) is extended to prove the new `Status`/cover fields still don't feed
`GetBkrRatioQuery`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director builds and publishes a weekly rota (Priority: P1)

The director plans next week's staffing in the existing "Rooster" web grid, now aware of each
staff member's contracted days and location closures (both greyed out so the director can't
mis-schedule), and publishes the week when it's ready — making it visible to staff for the first
time. Without this, no schedule ever reaches a staff member, so it's the foundation every other
story depends on.

**Why this priority**: Nothing else in this feature has anything to show until a rota exists and
is published — this is the prerequisite value delivery, and it's also independently useful today
(replacing 012's "every entry is immediately visible, no planning/review window" gap with an
actual draft-then-publish cycle).

**Independent Test**: As a director, build a week of assignments in the Rooster grid for a
location, confirm non-working days (per each staff member's contracted days) and closure days
are greyed and unselectable, publish the week, and confirm via the API that
`GET /api/staff-schedules/me` (as that staff member) now returns those rows — fully testable
without the staff app existing yet.

**Acceptance Scenarios**:

1. **Given** a staff member's contracted days are Mon/Tue/Wed, **When** the director opens the
   Rooster grid for a week including Thursday, **Then** Thursday's cell for that staff member is
   greyed and cannot receive a new assignment.
2. **Given** a location has a closure day (feature 011) within the displayed week, **When** the
   director views the grid, **Then** that entire column is greyed for every staff member at that
   location and cannot receive a new assignment.
3. **Given** a director has built a week's assignments but not yet published, **When** a staff
   member's own schedule is queried, **Then** none of that week's entries are returned.
4. **Given** a director publishes a week, **When** a staff member's own schedule is queried
   afterward, **Then** that week's entries are returned and each affected staff member receives a
   push notification.

---

### User Story 2 - Staff views their own schedule in the personal app (Priority: P2)

A staff member opens the new personal app on their phone and sees which location/group/hours
they're working over the next 4 weeks, in a day or week view.

**Why this priority**: This is the feature's headline motivation ("where am I working next
Wednesday?") — the first thing a staff member actually experiences, depending on P1's published
data existing.

**Independent Test**: Log in to `staff-mobile` as a staff member with a published schedule
(seeded via P1's flow), confirm the week and day views both show the correct location/group/
times, confirm a non-contracted day displays as such, and confirm a closure day shows "KDV
gesloten."

**Acceptance Scenarios**:

1. **Given** a staff member has published assignments in the next 4 weeks, **When** they open the
   app, **Then** they see those assignments grouped by day, with location, group, and time.
2. **Given** a staff member is scheduled at two locations on the same day, **When** they view
   that day, **Then** both assignments are shown.
3. **Given** a staff member has no published assignments for the visible range, **When** they
   open the app, **Then** they see an explicit "no shifts scheduled" empty state, not a blank
   screen or an error.
4. **Given** a staff member views a colleague's data is never requested by the client, **When**
   the app calls the schedule API, **Then** the server returns only the authenticated staff
   member's own rows regardless of any request parameter.

---

### User Story 3 - Staff reports sick; director arranges on-the-fly cover (Priority: P3)

A staff member wakes up sick and taps "Ik ben ziek" in the app. The director immediately sees an
urgent banner in the web admin, picks a replacement from a list of eligible, available staff, and
both the sick staff member and the cover both get notified.

**Why this priority**: The second headline scenario ("I need to call in sick") — depends on P1/P2
existing (a schedule to report sick *against*), but is the feature's other core differentiator
versus D-care.

**Independent Test**: As a staff member with a published assignment today, tap "Ik ben ziek";
confirm the director web admin shows an urgent cover-needed banner; confirm the eligible-staff
list excludes anyone already assigned to a conflicting group and anyone ineligible for that
location; assign a replacement; confirm the original assignment's status flips to absent, a new
`covered` assignment is created for the replacement, and both staff members receive a push
notification.

**Acceptance Scenarios**:

1. **Given** a staff member is scheduled today, **When** they tap "Ik ben ziek" and confirm,
   **Then** their assignment status becomes absent and the director sees an urgent banner without
   needing to refresh manually on next load.
2. **Given** the director opens the cover-assignment prompt, **When** the eligible-staff list is
   built, **Then** it excludes staff without `StaffLocationEligibility` for that location and
   staff already assigned to a conflicting group/time that day.
3. **Given** the director assigns a replacement, **When** the assignment is saved, **Then** the
   new entry is immediately visible to the replacement (bypassing the normal publish/draft gate,
   since this is an urgent operational change, not forward planning) and both staff members are
   notified.
4. **Given** a published week already exists, **When** the director makes any last-minute change
   to it (not just sick cover), **Then** the affected staff member receives a "Je rooster is
   gewijzigd" push notification.

---

### User Story 4 - Staff submits a planned leave request; director approves or rejects (Priority: P4)

A staff member requests annual/other leave for a future date range from the app. The director
reviews a "Verlofaanvragen" queue and approves or rejects; an approval automatically marks the
affected days absent on the rota.

**Why this priority**: Real value, but planned leave is lower-urgency than sick reporting and
fully additive — nothing else in this feature depends on it.

**Independent Test**: Submit a leave request for a future date range from the staff app; confirm
it appears in the director's Verlofaanvragen queue as pending; approve it; confirm the affected
rota days flip to absent and the staff member is notified; repeat with a rejection and confirm no
rota change occurs.

**Acceptance Scenarios**:

1. **Given** a staff member submits a leave request, **When** the director opens the
   Verlofaanvragen queue, **Then** the request appears with type, date range, and note.
2. **Given** the director approves a leave request, **When** the decision is saved, **Then**
   every affected date's assignment (if any) is marked absent and the staff member is notified.
3. **Given** the director rejects a leave request, **When** the decision is saved, **Then** no
   rota entries change and the staff member is notified of the rejection.
4. **Given** a staff member has a pending leave request, **When** they view their own requests in
   the app, **Then** they see its status (pending/approved/rejected).

---

### Edge Cases

- **Split day** (two locations, same date): both assignment rows are independent and both
  surface in the staff app (User Story 2, Scenario 2).
- **Last-minute change after publish**: bypasses draft/publish gating and pushes immediately
  (User Story 3, Scenario 4) — publish only gates *forward-planning* visibility, not
  already-published-week corrections.
- **Public holiday on a working day** (not a KDV closure — Belgian public holidays vary and this
  codebase has no legal-holiday calendar anywhere yet): no automatic detection or blocking. The
  director can use the existing free-text `Notes` field on an assignment for context; building a
  dedicated public-holiday calendar is out of scope for this feature (see Assumptions).
- **Staff member with no `StaffProfile`** (e.g. a `Director`-role user with no linked staff
  record): schedule/leave endpoints 404 with the existing `errors.staff.profile_not_found` key,
  matching `GetMyScheduleQuery`'s current behavior.
- **Leave request spanning a mix of scheduled and unscheduled days**: approval marks absent only
  the dates that actually have an assignment; dates with no assignment have nothing to mark.
- **Concurrent cover assignment** (two directors racing to assign cover for the same absence):
  the second attempt must fail cleanly rather than double-book a replacement — reuse the existing
  `IAdvisoryLockService` pattern feature 012's create/update already applies per staff member.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST let a director publish or unpublish a given week's rota for a
  location; only published entries are visible to the staff member they belong to.
- **FR-002**: The system MUST greyed out (non-selectable for new assignments) any day outside a
  staff member's contracted days, and any day the location is closed (feature 011), in the
  director-web rota grid.
- **FR-003**: The system MUST let a staff member view only their own schedule for the next 4
  weeks, in both a day view and a week view, via the personal staff app.
- **FR-004**: The system MUST show closed-location days in the staff app as "KDV gesloten" rather
  than as an empty/unscheduled day.
- **FR-005**: The system MUST let a staff member report themselves sick for today or tomorrow
  with a single confirmed tap, immediately flipping their assignment status to absent (if one
  exists for that date) and alerting the director.
- **FR-005a**: A sick report for a date that is already `Absent` (e.g. a repeated tap) MUST be
  idempotent — it MUST NOT create a duplicate `StaffLeaveRequest` or re-trigger a fresh director
  alert for the same already-reported day.
- **FR-006**: The system MUST show the director an urgent "who covers this?" prompt when a staff
  member is marked absent same-day, listing only staff who are eligible for that location
  (`StaffLocationEligibility`) and not already assigned to a conflicting group/time that day.
- **FR-007**: The system MUST, when a director assigns cover, mark the original assignment
  absent, create a new `covered`-status assignment for the replacement, and notify both staff
  members — bypassing the normal publish/draft gate for this specific change.
- **FR-008**: The system MUST notify the affected staff member by push notification whenever: a
  week is published, an already-published assignment changes, or a leave request is
  approved/rejected.
- **FR-008a**: A push notification's content MUST be limited to the recipient's own assignment
  details (location, group, date, time) and MUST NOT include another staff member's personal
  details beyond what's operationally required to confirm coverage is arranged (e.g., the
  originally-absent staff member's "your shift is covered" notification confirms coverage
  exists; it does not need to name the replacement).
- **FR-009**: The system MUST let a staff member submit a planned leave request (type: annual or
  other; date range; optional note) from the staff app.
- **FR-010**: The system MUST let a director view a queue of pending leave requests and approve
  or reject each one.
- **FR-011**: The system MUST, on leave-request approval, mark every date in the range that has
  an existing assignment as absent, and leave dates without an assignment untouched.
- **FR-011a**: A leave-request approval MUST NOT overwrite a `StaffSchedule` row that is already
  `Covered` for that date — a covered absence already has an arranged replacement, and silently
  reverting it to a plain `Absent` status would discard the recorded `CoverStaffId` and the
  cover arrangement it represents. Such dates are skipped by the approval (same as an
  unscheduled date), not overwritten.
- **FR-012**: The system MUST let a staff member view the status (pending/approved/rejected) of
  their own leave requests, and MUST NOT expose another staff member's leave requests to them.
- **FR-013**: The system MUST authenticate staff app users via the existing personal
  email/password JWT path (`role=staff`) — never the shared caregiver-tablet device token.
- **FR-014**: The system MUST prevent any assignment or cover-assignment write (create, update,
  cover) that violates `StaffLocationEligibility` — reusing feature 012's existing check.
- **FR-015**: The system MUST NOT let a schedule/leave-request read for one staff member return
  another staff member's rows, regardless of client-supplied identifiers — the staff identity is
  always resolved from the JWT, never a request parameter.
- **FR-015a**: The system MUST resolve the acting staff member's identity from the JWT for every
  staff-initiated write in this feature (sick report, leave-request submission) the same way
  FR-015 requires for reads — a staff-facing write endpoint MUST NOT accept a client-supplied
  staff/profile identifier as the record owner.
- **FR-016**: The system MUST continue to compute feature 010's live BKR ratio exclusively from
  `RoomShifts` check-in presence — the new `Status`/`CoverStaffId` fields on `StaffSchedule`
  MUST NOT be read by `GetBkrRatioQuery` (extends the existing decoupling guarantee from feature
  012 to the newly added fields).
- **FR-017**: All user-facing strings in both the extended director-web screens and the new
  staff app MUST use i18n keys, with Dutch, French, and English translations provided.
- **FR-018**: Serializing an assignment create/cover-assignment for the same staff member MUST
  remain race-safe under concurrent requests, reusing the existing per-staff advisory-lock
  pattern.

### Key Entities

- **StaffSchedule** *(extends feature 012's existing entity, does not replace it)*: a planned
  work assignment for one staff member on one date at one location (and optionally one group).
  Adds to the existing `Id, StaffProfileId, LocationId, GroupId, Date, StartTime, EndTime,
  CreatedAt, UpdatedAt`: a **Status** (`Scheduled` default / `Confirmed` / `Absent` / `Covered`,
  reconciled with — not duplicating — the existing `IsAbsent`/`AbsenceReason` fields, which this
  feature's plan phase must resolve into a single source of truth rather than two parallel
  absence signals), **CoverStaffId** (nullable, who replaced an absent staff member),
  **Notes** (free text), **CreatedBy** (which director created/changed the entry),
  **IsPublished**/**PublishedAt** (whether this entry is currently visible to its own staff
  member).
- **StaffLeaveRequest** *(new)*: a staff-initiated request for planned time off — staff member,
  type (sick/annual/other), date range, optional note, status (pending/approved/rejected),
  who decided it and when.
- **StaffProfile.ContractedDays** *(extends feature 005's existing entity)*: which weekdays a
  staff member is normally expected to work — drives the grey-out behavior in both the director
  grid and the staff app's own view.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A staff member can identify their next scheduled work location and time within 5
  seconds of opening the app.
- **SC-002**: Reporting a sick day takes no more than 2 taps (action + confirm) from the app's
  home screen.
- **SC-003**: A director can find and assign eligible cover for a same-day absence in under 60
  seconds from the moment the urgent banner appears.
- **SC-004**: 100% of schedule changes to an already-published week reach the affected staff
  member as a push notification within the existing Expo push delivery path (no polling
  required to learn of a change).
- **SC-005**: Zero cross-staff data exposure — a staff member's schedule/leave-request read never
  returns another staff member's rows, verified by a dedicated regression test (mirrors
  feature 012a's equivalent "can't read someone else's" guarantee).

## Assumptions

- **Publish granularity** is per (location, week) — matching feature 012's existing
  Monday-anchored `copy-week` convention — rather than per-day or per-entry; a director publishes
  or leaves in draft an entire week at a time for a location.
- **"Ik ben ziek" cutoff for "today or tomorrow"**: reports made before a location's normal
  opening time apply to today; reports made after count as tomorrow's notice, since same-day
  cover past opening time is director-handled urgently regardless (User Story 3), while an
  evening report is genuinely next-day planning. Exact cutoff time is a plan-phase configuration
  detail, not a product decision requiring its own clarification.
- **Public holidays are out of scope as a tracked concept** for this feature (see Edge Cases) —
  no calendar, no auto-flagging. The BACKLOG prompt's own wording ("no auto-block... director
  decides per-location") confirms a manual, low-ceremony approach is sufficient; a dedicated
  Belgian public-holiday calendar is future scope if a later feature needs one.
  **Extends** `Workflows/classroom-operations.md`
- **`StaffSchedule.IsAbsent`/`AbsenceReason` reconciliation** with the new `Status`/cover model is
  a plan-phase data-migration decision (in-place column addition + a value backfill from the
  existing boolean/enum), not a product-requirements question — no existing production tenant
  data exists yet in this pre-revenue project, so no live-data migration risk applies.
  Reconciling now (rather than leaving both) avoids the "two parallel absence representations"
  problem this feature's own corrected premise flags.
- **`staff-mobile` is a new, minimal Expo project** — it does not reuse `mobile` (caregiver
  tablet, kiosk/landscape/shared-device model) or `parent-mobile` (different domain/timeline
  content) as a base; it follows the same project-setup conventions (theme/colors.js,
  i18n scaffolding, openapi-fetch client generation) both existing apps already established.
- **Leave request `type` includes `sick`** per the original BACKLOG sketch, but User Story 3's
  "Ik ben ziek" one-tap flow is the primary path for same-day sick reporting; a staff member can
  also file a `sick`-typed leave request for a **future** known absence (e.g. a scheduled minor
  procedure) through the planned-leave form — the one-tap button and the leave-request form are
  two entry points into related but distinct urgency tiers, not duplicate features.
- Depends on: feature 012 (caregiver scheduling — `StaffSchedule`, `StaffLocationEligibility`,
  `GetBkrRatioQuery` decoupling), feature 005 (`StaffProfile`), feature 011 (closure calendar),
  feature 014a (`IExpoPushSender`/`Notification`/`NotificationType` push infrastructure).
