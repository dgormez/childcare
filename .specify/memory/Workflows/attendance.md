# Attendance Workflow

## Purpose

Allow childcare staff to know which children are present and responsible for them at any
moment, and to track BKR (begeleider-kind-ratio) compliance live as children check in and out.

### Trigger

A child arrives or leaves childcare.

### Actors

- Parent
- Caregiver
- Director
- System (offline sync, closure-day auto-absence — the latter added by a future feature)

### Flow

1. Parent brings child to center.
2. Caregiver taps the child's card on the tablet group view — pre-populated for the day from the
   child's contracted schedule (feature 010) — to confirm arrival. A child not covered by their
   contract for today's weekday can still be checked in manually (an "extra day").
3. System creates or updates the day's `attendance_record` for that child/location — one record
   per child per location per day. Online: written directly. Offline: queued via feature 008's
   offline sync (server-wins conflict policy; a duplicate check-in for the same child/day is
   rejected as a conflict, not retried).
4. Child status changes to present; check-out later sets the departure time on the same record.
5. The caregiver tablet shows a live, colour-coded BKR indicator (green/amber/red, never colour
   alone) — present children at the location ÷ qualified staff currently checked in via feature
   008a's room shift roster (students/volunteers excluded from the staff count). Thresholds: 8
   solo / 9 per caregiver normally, relaxed to 14 per caregiver when nap time is inferred (at
   least half of present children have an open sleep event, feature 009). The 18-cap leefgroep
   regime is not implemented — no group is flagged as a leefgroep in the data model yet. Display/
   warning only in Phase 1 — a breached ratio never blocks check-in.
6. Caregiver or director may instead mark a child absent for the day, classified justified
   (pre-approved) or unjustified, with an optional reason.
7. Parent receives confirmation if enabled (not built by feature 010 — no parent app exists yet).
8. Director reporting updates; a director can correct any record regardless of age (missed
   check-out, wrong status); a caregiver can correct only same-day records from the room's own
   tablet.

Not yet implemented, reserved for later features: bulk auto-marking `status = closure` from a
KDV closure calendar (feature 011 — the `closure` status value and the "no manual check-in
against a closure record" rule already exist as of feature 010) and parent-initiated
exchange/extra-day requests appearing here once approved (feature 013 — the underlying "extra
day" check-in capability already works, feature 013 only needs to add the request/approval UI).

### Applications

Caregiver Tablet:

- View expected arrivals (from contracted schedule).
- Mark arrival / departure, one tap each.
- Mark absence (justified/unjustified), a separate deliberate action from check-in.
- Live BKR ratio indicator.
- Correct mistakes, same day only.

Parent Mobile:

- View attendance status. (Not built — no parent app exists yet; feature 013's concern.)

Director Web:

- Monitor attendance.
- Review history.
- Correct any record regardless of age.

### Data

Attendance record:

- Child.
- Location.
- Date.
- Status (present / absent / closure).
- Check-in time.
- Check-out time.
- Planned duration in minutes (derived from the contract's schedule for that weekday; null when
  no contracted entry exists for an extra day).
- Absence justified (boolean) + reason, when absent.
- Who was checked in at the time of the action (from the room shift roster — a device-token
  action has no single individual identity to attribute to, same constraint as child events).
