# Child Lifecycle Workflow

## Purpose

Manage the relationship between a child and the childcare organization, from the moment a
family first expresses interest through enrollment, ongoing care, and eventual departure.

### Trigger

A family wants a place for their child, or an already-enrolled child's status changes
(room assignment, transfer, departure).

### Actors

- Director (registers waiting-list entries, prioritizes the queue, transitions status, manages
  enrollment, child profiles, room assignment)
- Parent/Contact (the family expressing interest — a data subject in feature 012a, not yet an
  app user; self-registration is feature 023)
- System (computes projected occupancy, sends offer-notification emails)

### Flow — pre-enrollment waiting list (feature 012a)

1. A family expresses interest in a place at a specific location, currently by contacting the
   center directly (phone/in person) — there is no public self-registration form yet (that's
   feature 023).
2. Director registers a lightweight waiting-list entry: child name, date of birth, contact
   details, desired location, requested start date. No child profile or contract exists yet —
   the entry is intentionally minimal.
3. Director prioritizes the queue for that location (manual ordering, per-location — occupancy
   and offers are always location-specific, so priority never spans locations).
4. When a place is likely available, director checks the occupancy view for that location: for
   each date, projected free capacity is `Location.MaxCapacity` minus active contracts (007)
   whose contracted weekdays cover that date, with any published closure day (011) shown as
   `Closed` rather than a numeric count — never real-time attendance, which doesn't exist yet
   for a future date.
5. Director marks the entry `offered` and contacts the family; the system sends an email
   notification to the contact if an email address is on file.
6. Family responds. Director marks the entry `enrolled` (a contract, feature 007, is created
   separately) or `withdrawn` (declined/cancelled) — both are terminal for that entry. A family
   that withdraws and later reapplies gets a brand-new entry, not a reopened old one.
7. On enrollment, the director links the entry to an existing child profile (006) if one
   exists, or is prompted to create one pre-filled from the entry's name/DOB. The child profile
   and any contract are separate records — the waiting-list entry is a historical trace of how
   the family got there, not a live source of truth once enrolled.

### Flow — ongoing lifecycle (no detail file content yet beyond enrollment)

Room assignment, transfers, and departure are covered by feature 006 (child profile, group
assignment with date ranges) and feature 007 (contracts, versioning, ending) — no additional
detail is added here until a feature actually needs it, per this document's governance rules.

### Applications

Director Web:

- Register, view, filter, and prioritize waiting-list entries per location.
- View projected occupancy per location, honoring closure days.
- Transition a waiting-list entry through its status lifecycle.
- Link an enrolled entry to an existing or newly created child profile.

Caregiver Tablet / Parent Mobile:

- Not involved in feature 012a. A future parent-facing self-registration surface is feature 023.

### Data

WaitingListEntry (feature 012a):

- Child first/last name, date of birth (no child profile yet — deliberately lightweight).
- Contact name, email, phone.
- Location (required — occupancy and priority are always per-location).
- Requested start date, priority (per-location ordering), status (`waiting` / `offered` /
  `enrolled` / `withdrawn` — terminal once `enrolled` or `withdrawn`), notes.
- Optional link to an existing `Child` (006), set only on enrollment, never auto-matched.
