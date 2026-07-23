# Classroom Operations Workflow

## Purpose

Manage how caregivers run a childcare room day-to-day — who is physically present and
accountable for it, on top of the schedule/staffing/ratio concerns the workflow map describes.
First concrete content: the room shift register (feature 008a).

### Trigger

A caregiver arrives at or leaves a room; a director sets up or reassigns a room tablet.

### Actors

- Caregiver
- Director
- System (auto-checkout, device-token rotation/revocation enforcement)

### Flow — room shift register (feature 008a)

1. Director pairs a tablet to a location + group (one-time setup).
2. Caregiver checks in with a PIN on arrival; checks out with the same PIN on leaving.
3. Multiple caregivers can be checked in to the same room simultaneously — the norm, not an
   edge case (BKR ratios typically require 2+ caregivers per room).
4. Any authenticated action taken on the tablet is attributed to whoever is checked in at that
   moment, without requiring a fresh per-action login.
5. System auto-closes shifts left open past a daily cutoff; a director corrects afterward.

### Applications

Caregiver Tablet:

- Room home screen: PIN keypad, "who's here" list.
- Kiosk lock — the daily entry point, replacing feature 008's personal login screen.

Director Web:

- Room/tablet pairing and revocation (Devices section).
- Caregiver PIN management (staff screen, feature 005 extended).

Parent Mobile:

- Not involved.

### Design Principles

- Presence tracking, not session ownership — the tablet is a shared room fixture, not a
  personal device any one caregiver "logs into."
- Two auth layers, never conflated: a device token (tablet ↔ backend security boundary) and a
  shift log (caregiver presence/accountability) — losing sight of this distinction is the
  single easiest way to get this workflow wrong.

### Data

- Device (paired tablet): tenant, location, group, device token state, revocation status.
- Room shift: caregiver, location, group, check-in time, check-out time (nullable).
- Group capacity (feature 018): a director sets/changes a group's capacity from the existing
  director-web Groups screen (`PATCH /api/groups/{id}/capacity`) — the only consumer is the
  Reporting & Management workflow's occupancy colour-coding (`Workflows/reporting.md`); groups
  themselves are still created only from the caregiver tablet's room-setup flow (008a), not web.
  Change note: added mid-implementation of feature 018 — the reporting spec needed a way to set
  the new `Group.Capacity` field director-side, and none existed for any group field before this.

### Flow — weekly staff rota (feature 012)

Distinct from the room shift register above: the rota is the *planned* schedule (who is
expected where, and when), while the shift register is the *actual* presence log (who
physically checked in). The rota feeds ratio planning ahead of time; the shift register
confirms it in the moment.

1. Director builds the upcoming week's rota in web admin: assigns staff members to
   locations, groups, and shift hours per day.
2. Director copies a prior week's rota forward rather than rebuilding it — the common case,
   since most KDVs run a stable weekly pattern. Entries falling on a closure day (see
   Attendance & Presence workflow) are excluded from the copy, not silently created.
3. Director marks a staff member absent (sick/leave/holiday) for a given day, removing them
   from that day's on-duty eligibility.
4. System computes a *projected* on-duty count from the rota (schedule + absence + staff
   qualification) for the rota builder's own planning display. This is separate from — and
   does not feed — feature 010's live BKR ratio, which is sourced from actual `RoomShifts`
   check-in presence (008a) and already correctly excludes anyone who hasn't physically
   checked in, scheduled or not.
5. A caregiver's own schedule is readable via a personal-account-scoped API. Feature 027 (Staff
   App) builds the personal-session-hosting surface that consumes it — see below.

### Flow — personal staff app & leave requests (feature 027)

Fulfills this workflow's own deferred pointer above: a personal schedule view needs a personal
session, which the shared kiosk tablet structurally doesn't have, so it ships as a new,
separate Expo app (`staff-mobile`) rather than on the tablet.

1. The rota (feature 012's `StaffSchedule`) gains a **publish/draft** state: a director's
   in-progress week is invisible to staff until explicitly published, at which point every
   affected staff member is notified.
2. A director's later change to an **already-published** week (most commonly, on-the-fly sick
   cover) bypasses the draft gate and reaches the affected staff member immediately — publish
   only gates forward-planning visibility, not corrections to a week staff can already see.
3. Staff can also submit a **leave request** (sick / annual / other) for a date range from their
   own app — a new, staff-initiated counterpart to the director-initiated absence-marking this
   workflow already had. A director reviews a queue and approves or rejects; approval marks the
   affected dates absent on the rota the same way director-initiated absence-marking already
   does, so both entry points converge on one absence representation, not two.
4. Same-day sick reporting is a distinct, higher-urgency one-tap action (not the leave-request
   form) specifically because it needs to reach the director fast enough to arrange cover before
   the shift starts.

### Applications — weekly staff rota

Director Web:

- Weekly rota builder (staff × day grid), rota copy, absence marking, publish/unpublish
  (feature 027), on-the-fly cover assignment (feature 027), leave-request approval queue
  ("Verlofaanvragen", feature 027).

Caregiver Tablet:

- Not involved — no personal schedule view exists on the shared kiosk tablet by design.

Parent Mobile:

- Not involved.

Staff Mobile (feature 027 — new, personal, distinct from the caregiver tablet):

- Own published schedule (day/week view), one-tap sick report, leave-request submission and
  status, a notifications list for publish/change/decision events.

System:

- The rota builder computes its own projected on-duty count from schedule + absence +
  qualification data, for director planning only. Feature 010's live BKR computation is
  unaffected — it consumes real-time `RoomShifts` check-in data, not the rota, and this remains
  true of every field feature 027 adds to `StaffSchedule` (status, cover, publish state).
- Push notifications (Expo, reusing the existing `IExpoPushSender`/`Notification` pattern from
  feature 014a) on: week published, an already-published assignment changed, leave request
  decided.

### Design Principles — weekly staff rota

- Planned vs. actual are two different records, not one — the rota (feature 012) states
  intent; the room shift register (feature 008a, above) states fact. Don't conflate them into
  a single table or query.
- A personal schedule view requires a personal session — the shared kiosk tablet
  structurally doesn't have one (see the room shift register's own design principle above),
  so "own schedule" visibility belongs on whichever surface has a personal login, not the
  tablet — feature 027's `staff-mobile` is that surface.
- One absence representation, not two — director-initiated absence-marking and staff-initiated
  leave-request approval both resolve to the same `StaffSchedule` status field (feature 027),
  rather than a second parallel "why is this person out" record.
- Publish gates planning visibility, not operational reality — once a week is published, urgent
  corrections (sick cover) reach staff immediately rather than waiting on a republish.
