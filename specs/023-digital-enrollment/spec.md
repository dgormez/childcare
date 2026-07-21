# Feature Specification: Digital Online Enrollment

**Feature Branch**: `023-digital-enrollment`

**Created**: 2026-07-21

**Status**: Draft

**Input**: User description: "Give parents a public enrollment form they can complete at any
time, without calling the KDV, and let them add themselves to a location's waiting list
directly — feeding into feature 012a's waiting list and pre-filling child/contact data when a
director converts an entry. Includes anti-spam protection (honeypot + IP rate limiting), a
parent-facing confirmation with a reference number, and a director-initiated tour-invitation
email with an accept/decline link and a manually recorded outcome."

## Product Context

### Feature Type

Mixed — a public, unauthenticated parent-facing form (User-facing UI), a director-web settings
toggle and review/conversion workflow (User-facing UI), and the backend capability that ties
them together (data-model change on `Location` and `WaitingListEntry`, plus new API surface).

### Primary Consumer

Mixed — Parent/Prospective Family (the public form; not yet an app user, per
`Workflows/child-lifecycle.md`'s existing note that this actor is "a data subject in feature
012a, not yet an app user") and Director (review, tour invitation, conversion).

### Workflow Boundary

This extends the **Child Lifecycle** workflow's existing pre-enrollment waiting-list flow
(`Workflows/child-lifecycle.md`, feature 012a) with the self-registration entry point that file
already anticipates ("there is no public self-registration form yet — that's feature 023").
`Workflows/child-lifecycle.md` is updated as part of this spec to describe the new entry point
and the tour-invitation step, per `workflows.md`'s governance rules (no existing business
meaning removed — this is additive to the same waiting-list status lifecycle 012a already
defines).

- **Actors**: Parent/Prospective Family (submits the form, no account), Director (reviews,
  prioritizes, sends tour invitations, converts), System (anti-spam enforcement, confirmation
  email, reference-number generation, director notification).
- **Actions**: parent submits the public form for a specific location → system creates an
  unverified `WaitingListEntry` (012a) → system emails a confirmation + reference number and
  notifies the director in-app → director reviews (duplicate-flagged if applicable), optionally
  sends a tour invitation, and eventually converts the entry to `offered`/`enrolled` (012a's
  existing status lifecycle, unchanged) with the child/contact creation flows pre-filled.
- **Data Flow**: extends `WaitingListEntry` (012a) with an origin marker (self-registered vs.
  director-entered), a reference number, the submission's selected locale, and tour-invitation
  state (proposed date/time, invitation status, manually recorded outcome) — the entry itself
  remains the same lightweight, pre-child-profile record 012a already defines. Extends
  `Location` with a per-location enable/disable flag and a public identifier for its distinct
  URL, mirroring the `Tenant.Slug` resolution pattern already used for organisation-scoped
  public links (`OrganisationSlugResolver`, feature 003).
- **Outputs**: an unverified `WaitingListEntry`, a parent-facing confirmation email (existing
  `EmailService`/`IEmailSender`, feature 020's templated-send pattern), a director-facing in-app
  `Notification` (existing entity, first use with a director as the recipient rather than a
  parent/contact), a tour-invitation email with a signed accept/decline link (mirrors
  `IUnsubscribeTokenService`'s signed, purpose-scoped token pattern).
- **Cross-platform Impact**: a new public, unauthenticated web page (director-web's Next.js app,
  new route outside the existing `(app)`/`(auth)` authenticated groups); director-web (location
  settings toggle, waiting-list UI additions — duplicate flag, tour-invite action, outcome
  entry); backend (new public endpoints + extensions to the existing waiting-list endpoints).
  No caregiver-tablet impact. No parent-mobile impact — the family has no app account at this
  stage, consistent with 012a's own scoping of this actor.

### User Impact

This enables a prospective family to join a specific KDV location's waiting list directly from
a public link, without phoning the center, and enables the director to follow up (via a tracked
tour invitation) and convert the entry into an enrolled child with pre-filled data — resulting
in a lower-friction top of funnel for both sides and less manual re-entry for the director.

### UX Requirements

**Persona**: Prospective Parent (primary, public form — an emotional, first-touch interaction
with the KDV, not yet a "user" of the operational product); Director (primary, review/
conversion queue and settings).

**Platform**: a new public web page (unauthenticated, one per location) + director-web
(settings, waiting-list).

**User job (parent)**: "I want to get my child on a KDV's waiting list without having to call
or visit during business hours."

**User job (director)**: "I want interest from the website to land directly in the waiting-list
queue I already use, pre-filled, so I don't retype it — and I want a low-pressure way to invite
promising families for a tour and keep track of whether they came."

**Success criteria**: per SC-001–SC-005 below — fast, mobile-friendly submission; zero behavior
change for locations that haven't opted in; zero retyped fields on conversion; reliable
anti-spam enforcement; and duplicate entries always surfaced, never silently dropped.

**Main flow (parent)**: public URL for a location → form (child first/last name, date of
birth, requested start date, parent/guardian name, email, phone, optional notes, language
toggle) → submit → confirmation screen showing the reference number, with the same information
also emailed.

**Main flow (director)**: in-app notification → the waiting-list view (012a) shows the new
entry tagged "self-registered," flagged as a possible duplicate if a name+DOB match exists at
the same location → director prioritizes as usual → optionally sends a tour invitation
(proposed date/time) → records the tour outcome once known → converts to `offered`/`enrolled`,
with the child-profile (006) and contact-creation flows pre-filled from the entry.

**Loading/empty/error states**: the submit action shows a clear loading state and disables
double-submission; validation errors are inline and field-specific (required fields, a date of
birth that isn't in the future); a rate-limited submission shows a calm, human-readable message
("please try again later"), never a raw HTTP status; a location with the public form disabled
shows a calm "not currently accepting online applications" message in place of the form, not a
broken page or a generic error.

**Accessibility**: this is a parent-facing form, not a caregiver one, so per
`design-system.md`'s Forms guidance it can be longer than a caregiver form but must still ask
for nothing unneeded; all inputs have associated labels, validation errors are announced (
`aria-live`) rather than color-only (per `design-system.md`'s "never convey a semantic state by
color alone" rule), and the language toggle is a visible, keyboard-operable control, not
detection-only.

**Offline behavior**: not applicable — this is a public web page loaded fresh per visit, the
same offline expectation (none) as the existing public login/reset-password pages. No mobile
app or offline-sync surface is involved.

**i18n**: NL/FR/EN throughout (form, confirmation email, tour-invitation email, and every new
director-facing label), via this codebase's existing i18n mechanisms — web's locale files for
UI copy, the existing `IEmailTemplateRenderer`/locale-keyed email pattern (feature 020) for
emails. The submitted locale — not the location's default — governs the confirmation and any
subsequent email to that contact, matching how `Contact.Locale` already governs email language
per-contact elsewhere in this codebase.

### Technical Requirements

**API impact**: a new unauthenticated public endpoint to submit an enrollment (scoped by
organisation + location identifier, mirroring how `OrganisationSlugResolver` already resolves a
tenant from a public slug before any authenticated lookup); new unauthenticated endpoints for a
tour invitation's accept/decline link (signed, purpose-scoped token, no login — same shape as
the existing unsubscribe/resubscribe endpoints). Director-facing: extensions to the existing
waiting-list endpoints for sending a tour invitation, recording its outcome, and toggling a
location's public-enrollment setting (mirrors the existing
`UpdateLocationCheckInSettingsCommand`-style per-location settings command pattern from features
008b/021).

**Data-model impact**:
- `Location` gains a public-enrollment-enabled flag, defaulting to `false` for every existing
  and new location (opt-in, matching the default-off convention this codebase has used for
  every prior per-location capability toggle — 008b, 014a, 021, 030), and a public identifier
  for its distinct URL.
- `WaitingListEntry` (012a) gains: an origin marker distinguishing self-registered from
  director-entered entries (defaulting existing rows to director-entered, so nothing about
  012a's shipped behavior changes); a unique reference number; the submission's selected
  locale; and tour-invitation state (proposed date/time, invitation status, a manually recorded
  outcome) as a single evolving set of fields rather than a history log — mirroring feature
  022's explicit precedent that this codebase has no per-change history table pattern anywhere,
  and the closest analog (013h's catalog deactivation) uses attribution fields, not a log.
- Contact email becomes a required field specifically for self-registered submissions (needed
  to deliver the confirmation and reference number), without changing 012a's existing
  director-entered flow, where contact email remains optional.
- An EF Core migration for the above, with a manually run SQL script per this repo's production
  convention (`.claude/CLAUDE.md`).

**Security considerations**: no authentication on the submission endpoint by design (the
prompt's own key constraint) — all input is validated and HTML-encoded before ever reaching a
director's screen (matches the existing `WebUtility.HtmlEncode` pattern already used for other
user-submitted free text, e.g. bulk-email/announcement bodies); a hidden honeypot field rejects
bot submissions without creating an entry, while still returning a generic success response so
the rejection isn't observable to the submitter; IP-based rate limiting reuses the existing
`AddRateLimiter`/sliding-window policy pattern already established in `Program.cs` for other
unauthenticated endpoints (auth, refresh); tour-invitation accept/decline links use a signed,
purpose-scoped token (mirrors `IUnsubscribeTokenService`) rather than a guessable entry ID; no
child or contact data is written to the tenant's authoritative `Child`/`Contact` records until a
director explicitly converts the entry — this is 012a's existing constraint, unchanged and
re-affirmed here for the self-registration path.

**Performance considerations**: the public endpoint must degrade gracefully under low-effort
bot traffic within the rate-limit budget; otherwise this is low-volume, prospective-family-scale
traffic with no special performance target beyond the existing API baseline.

**Testing requirements**: rate-limit enforcement (a 4th submission from the same source within
the rolling window is rejected, the first 3 valid ones succeed); honeypot rejection (entry not
created, generic success still returned); duplicate-flagging (name+DOB match at the same
location surfaces the flag, does not block creation); disabled-location behavior (public page
shows the disabled state, and the submission endpoint itself rejects even a direct request);
reference-number uniqueness; tour-invitation token tests (valid, expired, tampered); director
conversion pre-fill (zero retyped fields for name/DOB/contact details); notification-created-
on-submission; and locale-respecting confirmation/tour emails.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Parent submits the public enrollment form (Priority: P1)

A prospective parent finds a KDV location's public enrollment link (e.g. shared from the
center's own website) and fills in their child's name, date of birth, requested start date,
their own contact details, and an optional note, picking their preferred language. They submit
without creating an account. They immediately see a confirmation with a reference number, and
the same confirmation arrives by email.

**Why this priority**: This is the entire reason the feature exists — without it, nothing else
in this spec has anything to act on.

**Independent Test**: Can be fully tested by submitting the public form for an opted-in
location and verifying a `waiting`-status entry appears in that location's waiting list, with a
confirmation email sent to the address provided.

**Acceptance Scenarios**:

1. **Given** a location has public enrollment enabled, **When** a parent completes and submits
   the form with valid data, **Then** a new waiting-list entry is created with status `waiting`,
   marked as self-registered, and the parent sees a confirmation screen with a reference number.
2. **Given** the same successful submission, **When** it completes, **Then** a confirmation
   email containing the reference number is sent to the address the parent provided, in the
   language they selected on the form.
3. **Given** a parent leaves a required field empty or enters an invalid date of birth (in the
   future), **When** they attempt to submit, **Then** the form shows a specific, inline error
   for that field and does not submit.
4. **Given** a bot fills the hidden honeypot field, **When** it submits, **Then** no waiting-
   list entry is created, but a generic success response is still returned so the rejection is
   not observable to the submitter.

---

### User Story 2 - Director reviews and converts a self-registered entry (Priority: P1)

A director receives an in-app notification about a new self-registered waiting-list entry. They
open the existing waiting-list view and see the new entry, tagged as self-registered, and
flagged as a possible duplicate if a child with the same name and date of birth is already on
that location's list. When ready, the director marks the entry `offered`, then `enrolled` — the
child-profile creation flow and the contact-creation flow open pre-filled with everything the
parent already submitted, so the director only needs to confirm, not retype.

**Why this priority**: Without this, User Story 1's submissions just accumulate unreviewed —
the director-side half of the loop is what actually turns interest into an enrolled child.

**Independent Test**: Can be fully tested by submitting a self-registered entry (User Story 1),
then, as a director, converting it to `enrolled` and verifying the resulting child profile and
contact record match the submitted data with no manual retyping required.

**Acceptance Scenarios**:

1. **Given** a new self-registered entry exists, **When** the director opens the waiting-list
   view, **Then** the entry is visibly distinguishable from director-entered entries.
2. **Given** a self-registered entry's child name and date of birth match an existing entry at
   the same location, **When** the director views the list, **Then** the new entry is flagged
   as a possible duplicate, and both entries remain independently visible and actionable — the
   system never auto-rejects either one.
3. **Given** the director transitions a self-registered entry to `enrolled`, **When** they open
   the child-profile creation flow, **Then** the child's name and date of birth are pre-filled
   from the entry, and the contact-creation flow is pre-filled with the parent's name, email,
   and phone, requiring only confirmation.

---

### User Story 3 - Director sends a tour invitation and records the outcome (Priority: P2)

From a waiting-list entry, a director sends a tour invitation email proposing a date and time,
with an accept/decline link the recipient can use without creating an account. Later, whether or
not the recipient responded, the director manually records what actually happened (e.g. the
family came, rescheduled, or didn't respond).

**Why this priority**: This is real value on top of the core submit-and-convert loop (User
Stories 1–2), but the waiting list is usable without it — a director can convert an entry
without ever sending a tour invitation.

**Independent Test**: Can be fully tested by sending a tour invitation from an entry, following
the accept/decline link as the recipient, and verifying the entry reflects that response; then
separately recording a manual outcome and verifying it's saved independent of whether a link
response was ever received.

**Acceptance Scenarios**:

1. **Given** a waiting-list entry with a contact email, **When** the director sends a tour
   invitation with a proposed date/time, **Then** an email is sent containing that date/time and
   an accept/decline link, and the entry records that an invitation was sent.
2. **Given** a recipient uses the accept or decline link, **When** they do so, **Then** the
   entry's invitation status reflects their response without requiring them to log in or create
   an account.
3. **Given** a tour has occurred (or not), **When** the director manually records the outcome on
   the entry, **Then** that outcome is saved regardless of whether the recipient ever used the
   accept/decline link.

---

### User Story 4 - Director temporarily disables public enrollment for a location (Priority: P3)

A location is at capacity with no projected availability. The director opens that location's
settings and turns off public enrollment. The public URL immediately stops accepting new
submissions and shows a calm message instead of the form. The director can re-enable it later
with no loss of previously submitted entries.

**Why this priority**: This is a safety valve, not core value — the feature functions correctly
without a director ever touching this setting, but it matters for the realistic operational case
of a full location.

**Independent Test**: Can be fully tested by disabling the setting for a location and verifying
the public page shows the disabled message and a direct submission attempt is rejected, then
re-enabling it and verifying the form works again with all prior entries intact.

**Acceptance Scenarios**:

1. **Given** a director disables public enrollment for a location, **When** anyone visits that
   location's public URL afterward, **Then** they see a clear message that online applications
   aren't currently accepted, not a form.
2. **Given** the setting is disabled, **When** a submission is attempted directly against the
   endpoint (bypassing the UI), **Then** it is rejected server-side — disabling is enforced, not
   just hidden.
3. **Given** the director re-enables the setting, **When** they save, **Then** the public form
   works again immediately, and every entry submitted before the setting was toggled off remains
   unchanged in the waiting list.

---

### Edge Cases

- A parent submits a duplicate entry (same child name + date of birth already on that location's
  waiting list). The new entry is still created and flagged to the director — never
  auto-rejected, since the family may have a legitimate reason to reapply (per 012a's existing
  precedent for handling re-application after `withdrawn`).
- A location is at capacity with no projected availability — the director disables the public
  form for that location (User Story 4) rather than the system doing so automatically; there is
  no automatic capacity-based disabling in this feature.
- A parent selects a language on the form that differs from the location's default — the
  submission's selected language governs their confirmation and any later email to them, not the
  location's default.
- A parent provides a phone number but the email field is left blank — rejected at submission
  time with an inline validation error, since email is required for self-registered entries (the
  only channel for the confirmation and reference number); this differs from 012a's own
  director-entered flow, where contact email remains optional.
- A director disables the form while a parent already has it open and mid-fill — the submission
  is rejected server-side at submit time with a clear message, not just prevented by hiding the
  page in advance.
- A tour-invitation accept/decline link is used twice, or after the entry has already moved past
  `waiting`/`offered` — the link records the response but never re-opens or alters an
  already-terminal (`enrolled`/`withdrawn`) entry.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a distinct, publicly accessible enrollment form URL per
  location, requiring no login or account to view or submit.
- **FR-002**: Public enrollment MUST default to disabled for every location, including all
  locations that existed before this feature ships, until a director explicitly enables it.
- **FR-003**: The public form MUST collect: child first name, child last name, date of birth,
  requested start date, parent/guardian name, parent/guardian email (required), parent/guardian
  phone, optional notes, and a language selector (Dutch/French/English).
- **FR-004**: The system MUST validate submitted dates (date of birth MUST NOT be in the future)
  and required fields before accepting a submission, showing field-specific inline errors
  otherwise.
- **FR-005**: The form MUST include a hidden honeypot field; a submission where it is filled
  MUST be silently discarded (no waiting-list entry created) while still returning a generic
  success response to the submitter.
- **FR-006**: Submissions MUST be rate-limited to a maximum of 3 per source IP address per
  rolling hour; a submission exceeding this limit MUST be rejected with a clear, human-readable
  message.
- **FR-007**: A successful, non-honeypot, non-rate-limited submission MUST create a
  `WaitingListEntry` (012a) with status `waiting` for the specified location, marked as
  self-registered and distinguishable from director-entered entries.
- **FR-008**: The system MUST generate a unique reference number for each self-registered entry
  and include it in the confirmation shown to the parent and the confirmation email sent to
  them.
- **FR-009**: The system MUST send a confirmation email immediately after a successful
  submission, in the language selected on the form.
- **FR-010**: The system MUST create an in-app notification for the location's director(s) when
  a new self-registered entry is created.
- **FR-011**: If a self-registered submission's child first/last name and date of birth match an
  existing waiting-list entry at the same location (any status), the new entry MUST still be
  created and MUST be visibly flagged as a possible duplicate on the director's waiting-list
  view — never auto-rejected.
- **FR-012**: Directors MUST be able to enable or disable public enrollment per location,
  independent of every other location.
- **FR-013**: When a location's public enrollment is disabled, its public URL MUST show a clear
  message that online applications aren't currently accepted, and the submission capability
  MUST be rejected server-side even if requested directly — disabling is enforced, not just a UI
  hide.
- **FR-014**: When a director transitions a self-registered entry to `offered` or `enrolled`
  (012a's existing status lifecycle, unchanged), the child-profile creation flow (006) and the
  contact-creation flow MUST be pre-filled from the entry's submitted data, requiring the
  director to confirm rather than retype.
- **FR-015**: Directors MUST be able to send a tour invitation email, from a waiting-list entry,
  containing a proposed date/time and an accept/decline link.
- **FR-016**: A tour invitation's accept/decline link MUST be usable without the recipient
  creating an account or logging in, and MUST record the recipient's response against that
  entry.
- **FR-017**: Directors MUST be able to manually record a tour's outcome on the entry,
  independent of whether the recipient ever used the accept/decline link.
- **FR-018**: A tour-invitation accept/decline response MUST NOT alter an entry that has already
  reached a terminal status (`enrolled` or `withdrawn`, per 012a's existing lifecycle).
- **FR-019**: All new user-facing text (public form, confirmation email, tour-invitation email,
  new director-facing labels) MUST be available in Dutch, French, and English via the existing
  i18n mechanism — no hardcoded strings.
- **FR-020**: No child or contact data from a self-registered entry MUST be written to the
  tenant's authoritative `Child` or `Contact` records until a director explicitly converts the
  entry — 012a's existing constraint, unchanged for this path.
- **FR-021**: The public submission and tour-response endpoints MUST NOT require or accept any
  authentication credential, and MUST NOT expose any existing waiting-list, child, or contact
  data to the requester beyond their own submission's confirmation.

### Key Entities

- **WaitingListEntry** (existing, feature 012a, extended): gains an origin marker (self-
  registered vs. director-entered, defaulting existing rows to director-entered), a unique
  reference number, the submission's selected locale, and tour-invitation state (proposed
  date/time, invitation status, manually recorded outcome) as evolving fields on the entry
  itself, not a separate history log.
- **Location** (existing, extended): gains a per-location enable/disable flag for public
  enrollment (defaulting to disabled) and a public identifier used to build that location's
  distinct enrollment URL.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A prospective parent can complete and submit the public enrollment form in under 3
  minutes on a typical mobile browser.
- **SC-002**: Zero existing locations show a public enrollment form immediately after this
  feature ships — every location requires an explicit director opt-in first.
- **SC-003**: A director converting a self-registered entry to an enrolled child retypes zero of
  the child's name/date-of-birth or the contact's name/email/phone.
- **SC-004**: More than 3 submission attempts from the same source within a rolling hour are
  rejected 100% of the time, while the first 3 valid attempts within that window always succeed.
- **SC-005**: 100% of self-registered entries that genuinely match an existing entry's child
  name and date of birth at the same location are flagged to the director without the director
  needing to manually search the list.

## Assumptions

- The reference number is used by the family to identify themselves when they contact the
  center by phone, email, or in person — this feature does not build a self-service public
  status-lookup page, since a full parent portal is explicitly out of scope per the feature's
  own "Out of scope" note.
- A location's default form language is a starting toggle position (a new, per-location default-
  language setting, defaulting to Dutch for existing locations) — but the parent's selected
  language, once changed on the form, governs their confirmation and any later email, matching
  how `Contact.Locale` already governs email language per-contact elsewhere in this codebase.
- Tour-invitation state is modeled as a single evolving set of fields on the waiting-list entry
  (latest invitation only), not a history log — mirroring feature 022's explicit precedent that
  this codebase has no per-change history table pattern anywhere.
- Duplicate detection compares only child first/last name and date of birth within the same
  location — matching 012a's own minimal entry shape — and does not attempt fuzzy/typo matching.
- The public form is served as a new unauthenticated route within the existing director-web
  (Next.js) app, not a separate marketing site — this is a transactional, tenant/location-scoped
  form, not brand-register marketing content (see `design-decisions.md`'s explicit distinction
  between the operational platform and a future, separate marketing surface).
- The in-app director notification (FR-010) targets every director account associated with the
  entry's location's organisation — this is the first feature to target a director as an
  in-app-notification recipient (every prior use of the existing `Notification` entity targets a
  parent/contact), reusing the same mechanism rather than inventing a second one.
- Online payment of a registration deposit and full parent-account/portal management remain out
  of scope, per the feature's own explicit exclusions.
