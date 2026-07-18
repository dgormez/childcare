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
5. A caregiver's own schedule is readable via a personal-account-scoped API, but has no
   caregiver-facing UI yet — the shared kiosk tablet (008a) has no personal session to host a
   personal view; that UI is feature 027's job (Staff App), not this workflow's tablet
   surface.

### Applications — weekly staff rota

Director Web:

- Weekly rota builder (staff × day grid), rota copy, absence marking.

Caregiver Tablet:

- Not involved — no personal schedule view exists on the shared kiosk tablet by design.

Parent Mobile:

- Not involved.

System:

- The rota builder computes its own projected on-duty count from schedule + absence +
  qualification data, for director planning only. Feature 010's live BKR computation is
  unaffected — it consumes real-time `RoomShifts` check-in data, not the rota.

### Design Principles — weekly staff rota

- Planned vs. actual are two different records, not one — the rota (feature 012) states
  intent; the room shift register (feature 008a, above) states fact. Don't conflate them into
  a single table or query.
- A personal schedule view requires a personal session — the shared kiosk tablet
  structurally doesn't have one (see the room shift register's own design principle above),
  so "own schedule" visibility belongs on whichever surface has a personal login, not the
  tablet.
