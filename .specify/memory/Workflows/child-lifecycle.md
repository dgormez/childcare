# Child Lifecycle Workflow

## Purpose

Manage the relationship between a child and the childcare organization, from the moment a
family first expresses interest through enrollment, ongoing care, and eventual departure.

### Trigger

A family wants a place for their child, or an already-enrolled child's status changes
(room assignment, transfer, departure).

### Actors

- Director (registers waiting-list entries, prioritizes the queue, transitions status, manages
  enrollment, child profiles, room assignment; also sends tour invitations and records their
  outcome — feature 023)
- Parent/Contact (the family expressing interest — a data subject in feature 012a, not yet an
  app user; may self-register directly via the public enrollment form, feature 023)
- System (computes projected occupancy, sends offer-notification emails, enforces public-form
  anti-spam protections, sends self-registration confirmation/tour-invitation emails — feature
  023)

### Flow — pre-enrollment waiting list (feature 012a; self-registration entry point added by
feature 023)

1. A family expresses interest in a place at a specific location, either by contacting the
   center directly (phone/in person — director registers the entry, as below), or, where a
   director has opted a location into it, by submitting a public, no-login enrollment form
   directly (feature 023). A self-registered submission creates the same kind of lightweight
   entry a director would, plus a reference number and a confirmation email to the family; it is
   flagged as a possible duplicate (never auto-rejected) if it matches an existing entry's child
   name and date of birth at the same location. Public enrollment defaults to disabled per
   location until a director opts in, and can be temporarily disabled again (e.g. at capacity).
2. Director registers a lightweight waiting-list entry: child name, date of birth, contact
   details, desired location, requested start date. No child profile or contract exists yet —
   the entry is intentionally minimal. (Self-registered entries arrive pre-filled with the same
   shape, per step 1.)
3. Director prioritizes the queue for that location (manual ordering, per-location — occupancy
   and offers are always location-specific, so priority never spans locations).
4. When a place is likely available, director checks the occupancy view for that location: for
   each date, projected free capacity is `Location.MaxCapacity` minus active contracts (007)
   whose contracted weekdays cover that date, with any published closure day (011) shown as
   `Closed` rather than a numeric count — never real-time attendance, which doesn't exist yet
   for a future date. Optionally, the director sends a tour invitation from the entry (feature
   023) — a proposed date/time and a no-login accept/decline link — and later records the tour's
   outcome manually, independent of whether the recipient used the link.
5. Director marks the entry `offered` and contacts the family; the system sends an email
   notification to the contact if an email address is on file.
6. Family responds. Director marks the entry `enrolled` (a contract, feature 007, is created
   separately) or `withdrawn` (declined/cancelled) — both are terminal for that entry. A family
   that withdraws and later reapplies gets a brand-new entry, not a reopened old one. A
   tour-invitation accept/decline response arriving after an entry has already reached one of
   these terminal statuses does not reopen or alter it (feature 023).
7. On enrollment, the director links the entry to an existing child profile (006) if one
   exists, or is prompted to create one pre-filled from the entry's name/DOB — for a
   self-registered entry (feature 023), this pre-fill also covers the contact-creation flow
   (name/email/phone), so the director only confirms rather than retypes. The child profile
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
- Enable/disable public enrollment per location, send tour invitations, and record tour outcomes
  (feature 023).

Public (unauthenticated), no app account required:

- A per-location public enrollment form (feature 023) a family submits directly, creating a
  waiting-list entry without director involvement; and a tour-invitation accept/decline link
  the recipient can use without an account.

Caregiver Tablet / Parent Mobile:

- Not involved. The family remains without an app account throughout the waiting-list stage,
  whether the entry originated from the director or from self-registration (feature 023).

### Data

WaitingListEntry (feature 012a; extended by feature 023):

- Child first/last name, date of birth (no child profile yet — deliberately lightweight).
- Contact name, email, phone (email required specifically for self-registered entries — it's
  the only delivery channel for their confirmation and reference number).
- Location (required — occupancy and priority are always per-location).
- Requested start date, priority (per-location ordering), status (`waiting` / `offered` /
  `enrolled` / `withdrawn` — terminal once `enrolled` or `withdrawn`), notes.
- Optional link to an existing `Child` (006), set only on enrollment, never auto-matched.
- Origin (director-entered vs. self-registered), reference number, and submission locale
  (self-registered entries only; feature 023).
- Tour-invitation state — proposed date/time, invitation status, manually recorded outcome — as
  a single evolving set of fields, not a history log (feature 023).
