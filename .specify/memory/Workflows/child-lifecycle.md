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
  outcome — feature 023; sends/resends contract signing invitations and configures the
  organisation's SEPA Creditor Identifier — feature 024)
- Parent/Contact (the family expressing interest — a data subject in feature 012a, not yet an
  app user; may self-register directly via the public enrollment form, feature 023; signs their
  child's enrolment contract and authorises a SEPA mandate via a no-login link — feature 024)
- System (computes projected occupancy, sends offer-notification emails, enforces public-form
  anti-spam protections, sends self-registration confirmation/tour-invitation emails — feature
  023; enforces signing-token single-use/expiry, generates and persists the signed contract PDF
  — feature 024)

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

### Flow — contract signing (feature 024)

1. Once a contract (007) exists for an enrolled child, in `Draft` status, the director sends a
   signing invitation — a secure, single-use, 72-hour link emailed to the family's contact
   address. This requires the organisation's SEPA Creditor Identifier to already be configured
   (a one-time organisation-settings step); a director cannot send an invitation without it.
2. The parent opens the link — no account or login — reviews the full contract terms, signs
   (drawn or typed), and authorises a SEPA direct debit mandate with their IBAN, all in the same
   session. The system generates a unique mandate reference, records the signature and mandate,
   invalidates the link, and generates and persists a final signed PDF (contract + signature +
   mandate) — this stored PDF is never regenerated from live data afterward, even if the
   contract is later amended.
3. If the link expires before the parent signs, the director resends — the prior link stops
   working the moment the new one is issued. If the director edits the Draft contract while a
   signing invitation is outstanding, that link is invalidated immediately; a new invitation
   reflecting the revision must be sent before the parent can sign.
4. Signing does not gate the existing `Draft → Active` transition (007) — a director can still
   activate a contract independent of whether it has been signed. A signed contract's terms are
   frozen: any revision after signing goes through 007's existing amendment mechanism (a new
   contract row), which then needs its own fresh signature.

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
- Send/resend a contract signing invitation, see a contract's signing status, and configure the
  organisation's one-time SEPA Creditor Identifier (feature 024).

Public (unauthenticated), no app account required:

- A per-location public enrollment form (feature 023) a family submits directly, creating a
  waiting-list entry without director involvement; and a tour-invitation accept/decline link
  the recipient can use without an account.
- A contract signing page (feature 024) a family reaches via a secure, time-limited emailed
  link, to review, sign, and authorise a SEPA mandate without an account.

Caregiver Tablet / Parent Mobile:

- Not involved. The family remains without an app account throughout the waiting-list and
  contract-signing stages, whether the entry originated from the director or from
  self-registration (feature 023), and whether signing happens via mobile browser or desktop
  (feature 024).

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

Contract (feature 007; extended by feature 024):

- Signing-token state (token, expiry), signed timestamp, signature (drawn/typed), signer IP.
- SEPA mandate: IBAN (encrypted at rest), system-generated mandate reference, mandate
  authorisation timestamp.
- A signed contract's terms are frozen — further edits are rejected; revision requires 007's
  existing amendment mechanism (a new contract row), which then needs its own signature.

Tenant (feature 001; extended by feature 024):

- SEPA Creditor Identifier — one per organisation, director-entered once, required before any
  signing invitation can be sent (mirrors the existing `KboNumber` field, feature 014).
