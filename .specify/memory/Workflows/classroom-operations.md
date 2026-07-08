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
