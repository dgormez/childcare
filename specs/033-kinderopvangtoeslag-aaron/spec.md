# Feature Specification: Kinderopvangtoeslag (AARON) Submission

**Feature Branch**: `033-kinderopvangtoeslag-aaron`

**Created**: 2026-07-24

**Status**: Draft

**Input**: User description: "Build monthly attendance submission to Opgroeien for the
kinderopvangtoeslag (Groeipakket childcare allowance) — for vrije-prijs (free-price) KDVs. REST/
JSON webservice (AARON backend), bearer-token auth, single endpoint POST /opvangprestaties.
Derive per-child presence periods from feature 010 attendance data, submit monthly per location,
store an immutable audit copy of what was sent and the response, surface errors for correction,
alert before the 7th-of-month deadline. Field-level contract verified against
`docs/integrations/opgroeien/kinderopvangtoeslag-aaron/KinderOpvangToeslag.json`."

## Product Context

### Feature Type

Mixed (Data-model change + API-backend capability, with a director-web review/submission/
alerting UI).

### Primary Consumer

Director (reviews and submits monthly, resolves errors, manages per-child opt-out). System
(derives the payload from attendance data, calls the Opgroeien webservice, stores the response).

### Workflow Boundary

**Government Reporting & Compliance** (`.specify/memory/workflows.md` /
`Workflows/government-reporting.md`) — explicitly listed there: "Monthly kinderopvangtoeslag
attendance submission (AARON webservice — feature 033)." This is the first feature to actually
implement that workflow item.

Actors: Director (reviews the derived payload before send, resolves validation errors, sets/
removes a child's opt-out flag). System (derives per-child presence periods from feature 010
attendance records for a location+month, builds the payload, calls AARON, stores the response).
Opgroeien/AARON (external recipient; validates each child against the Rijksregister and pays the
toeslag to the parent).

Actions: derive monthly presence data per location → director review screen (excluded/opted-out
children flagged, missing data flagged) → director submits → system stores an immutable audit
copy of the exact payload sent and the exact response received → errors surfaced per child/day →
director corrects the underlying attendance or child data → director resubmits → deadline
alerting fires if a location+month is unsubmitted or has unresolved errors as the 7th-of-month
deadline approaches.

Data Flow: feature 010 attendance records (check-in/check-out timestamps) for a location+month →
grouped per child → per-day presence periods → JSON payload (`locatieId`, `periode`,
`prestaties[].kind` with `kindNaam`/`kindVoornaam`/`kindGeboortedatum`/`kindGeslacht`/
`datumSchoolgaand`/`kindPrestaties[].datumOpvang`/`kindPrestatiesDetails[].checkIn`+`checkOut`) →
`POST /opvangprestaties` → `OpvangPrestatieResponseDTO` (`success`, `result`, `crlId`,
`errorList[]`) → submission log entry per location+month, one row per submission attempt.

Outputs: submission log (payload sent, response received, status, timestamp, submitted-by) per
location+month; a per-child/per-day error list on ERROR; a deadline-alert surfaced to the
director before the 7th of the following month; a per-child toeslag opt-out flag with a period,
visible on the child profile.

Cross-Platform Impact: Director web (primary — review screen, submission log, error resolution,
deadline alerting). Backend-only for the webservice client and payload derivation. No caregiver-
tablet or parent-mobile UI in this feature — displaying the child's AARON QR/numeric code inside
the parent app is a distinct future opportunity (tied to feature 021) and explicitly out of scope
here.

### User Impact

This enables a director to submit the legally required monthly childcare-attendance data to
Opgroeien directly from ChildCare instead of re-keying it into the separate AARON web app,
resulting in fewer missed deadlines, fewer fines, and parents reliably receiving their
kinderopvangtoeslag.

### UX Requirements

- **Persona**: Director, desktop web, high-density operational screen per `platform-rules.md`.
- **Platform**: director-web only, minimum 1280px viewport, mouse/keyboard interaction with
  visible focus rings and full keyboard reachability (no touch-target floor).
- **User job**: "Before the 7th of the month, confirm this month's attendance data is correct and
  submit it to Opgroeien without errors."
- **Success criteria**: the submission reaches Opgroeien with zero unresolved errors before the
  deadline; every submission attempt (including resubmissions) is auditable after the fact.
- **Main flow**: director opens the monthly submission screen for a location → reviews the
  derived per-child presence data (opted-out children shown as excluded, with why) → submits →
  sees the SUCCESS/ERROR result inline → on ERROR, sees the per-child, per-error-code list →
  corrects the underlying attendance or child data elsewhere in the app → returns and resubmits.
- **Loading/empty/error states**: a loading state while the payload is derived from attendance
  data; an empty state if no attendance has been recorded yet for the location+month; an error
  state that maps each `ErrorResponseDTO` (`errorNr`, `errorCode`, `errorText`) to the specific
  child and day it concerns, not a raw dump of the response.
- **Accessibility**: keyboard-reachable review/submit flow; visible focus rings; screen-reader-
  readable error list (not color-only — pairs with the `danger` icon per `design-system.md`).
- **Offline behavior**: not applicable — director-web is not offline-first. If the webservice
  call itself fails (network/5xx), the attempt is recorded as a retryable failed state, never
  silently dropped.

### Technical Requirements

- **API impact**: new backend endpoints to (a) derive/preview the monthly payload for a
  location+period, (b) submit it, and (c) list the submission log for a location. A webservice
  client for `POST /opvangprestaties` (bearer-token auth, base URL and token supplied per
  environment via configuration/secret, never hardcoded).
- **Data-model impact**: a submission log table (location, period, payload snapshot, response
  snapshot, status, submitted-by, timestamps) — one row per submission attempt, immutable once
  written. A per-child toeslag opt-out flag with an effective period (defaulting to birthdate,
  open-ended allowed). Child model fields required for the payload — official first/last name,
  gender, birthdate, optional school-start date — reusing feature 006's child model, extending
  only where a required field is missing rather than duplicating existing ones.
- **Security considerations**: bearer token stored as an environment/keyvault secret, provisioned
  via the deployment pipeline, never hardcoded or checked in. The payload contains
  Rijksregister-matchable PII (exact legal name, birthdate, gender) — handle with the same care
  as other child PII already in the system; the submission log's payload snapshot is subject to
  the same access control as the rest of a location's child data (director-only).
- **Performance considerations**: monthly batch derivation runs over a month of attendance
  records for a location; deriving the preview must not block the review screen from loading —
  the screen shows a loading state while derivation completes.
- **Testing requirements**: happy path (derive → review → submit → SUCCESS stored, audit copy
  retained) and key negative flows (ERROR response stored and surfaced per child/day; opted-out
  child excluded from the payload entirely; a location+month with no submission as the deadline
  approaches triggers an alert) per project conventions (xUnit/Moq).

### Source Documents

- `docs/integrations/opgroeien/kinderopvangtoeslag-aaron/KinderOpvangToeslag.json` — Swagger 2.0
  field-level contract for `POST /opvangprestaties` (test host `tstgpappr.kindengezin.be`).
- `docs/integrations/opgroeien/kinderopvangtoeslag-aaron/aaron-sector-handleiding.pdf` —
  organisator manual: 7th-of-month deadline, fines, Rijksregister/bisnummer validation, 2.5-year
  toeslag expiry, opt-out periods, location lifecycle handling.
- `docs/integrations/opgroeien/kinderopvangtoeslag-aaron/aaron-handleiding-ouders.pdf` — parent
  manual: QR/numeric codes, ≤5 contacts per child, exact-Rijksregister identity matching.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Submit a month's attendance to Opgroeien (Priority: P1)

A director opens the kinderopvangtoeslag submission screen for one of their locations at the end
of the care month. The system has already derived each enrolled child's daily presence periods
from the attendance register. The director reviews the list — every child shown, opted-out
children clearly marked and excluded from what will be sent — and submits. The submission
succeeds and the director sees a clear confirmation with a reference they can look up later.

**Why this priority**: without this flow the feature delivers nothing — this is the entire
reason the feature exists (replacing manual AARON re-entry with a direct submission).

**Independent Test**: can be fully tested by attendance-checking a child in/out on several days
within a month, opening the submission screen for that location+month, and submitting — the
submission log shows a SUCCESS entry with the sent payload and the response.

**Acceptance Scenarios**:

1. **Given** a location has attendance records for at least one child in the selected month,
   **When** the director opens the submission screen for that location+month, **Then** the
   screen shows one row per non-opted-out child with their derived daily presence periods.
2. **Given** the director reviews the derived data and it looks correct, **When** the director
   submits, **Then** the system sends the payload to Opgroeien, stores an immutable copy of the
   exact payload and the exact response, and shows the director a SUCCESS confirmation.
3. **Given** a child has an active toeslag opt-out for part or all of the selected month,
   **When** the payload is derived, **Then** that child (or the opted-out days) is excluded from
   what is sent, and the review screen shows why.

---

### User Story 2 - Resolve a submission error and resubmit (Priority: P1)

A director submits a month's data and Opgroeien responds with validation errors — for example, a
child's date of birth doesn't match what's on file with the Rijksregister. The director needs to
see exactly which child and which error, go fix the underlying data, and resubmit before the
deadline without starting over from scratch.

**Why this priority**: error resolution is not optional polish — the sector handleiding
describes an automatic reminder e-mail and a real fine risk for unresolved errors past the 8th of
the month, so this flow is as load-bearing as the happy path.

**Independent Test**: can be fully tested by submitting a payload containing a child with a known-
bad field (e.g. missing checkout time), observing the stored ERROR response with its per-child
error detail, correcting the underlying attendance record, and resubmitting to confirm a new
SUCCESS log entry is created without losing the record of the earlier failed attempt.

**Acceptance Scenarios**:

1. **Given** a submission attempt returns `result: "ERROR"` with one or more entries in
   `errorList`, **When** the director views the submission, **Then** each error is shown against
   the specific child and day it concerns, not as a raw response dump.
2. **Given** a director has corrected the underlying data after an ERROR, **When** the director
   resubmits the same location+period, **Then** the system creates a new, immutable submission
   log entry for the new attempt while retaining the prior failed attempt for audit purposes.

---

### User Story 3 - Get alerted before the submission deadline is missed (Priority: P2)

A director who has not yet submitted (or has an unresolved-error submission) for a location as
the 7th-of-the-month deadline approaches gets a clear alert, so the fine described in the sector
handleiding is avoidable rather than a surprise on the 8th.

**Why this priority**: prevents the costly failure mode (missed deadline, fine) but the platform
still delivers its core value without it on day one — a P1 gap here is recoverable manually for a
single month, whereas Story 1/2 gaps make the feature unusable at all.

**Independent Test**: can be fully tested by leaving a location+month unsubmitted as the
configured alert threshold before the 7th is reached, and confirming the director sees a
deadline-risk indicator without needing to separately check every location by hand.

**Acceptance Scenarios**:

1. **Given** a location has no submitted-and-SUCCESS entry for the previous care month as the
   deadline approaches, **When** the director views their dashboard, **Then** an alert identifies
   the at-risk location+month.
2. **Given** a location's most recent submission for a period is an unresolved ERROR, **When**
   the deadline approaches, **Then** the same alert treats it as at-risk (not as "submitted").

---

### Edge Cases

- What happens when a location has zero eligible children (no free-price attendance, or all
  children opted out) for a month? The review screen shows an explicit empty state rather than
  letting the director submit an empty/meaningless payload.
- How does the system handle a child who turns 2.5 years old partway through the covered month
  (toeslag right expiry, unless already attending school)? The derivation must reflect their
  eligibility only up to the relevant cutoff within that month.
- How does the system handle a location that stops, loses its licence, or relocates mid-month
  (per the sector handleiding's location-lifecycle notes)? The affected period is still derivable
  and submittable for the days the location was active; the review screen does not silently drop
  or fabricate data for the inactive days.
- What happens if the webservice call itself fails (network error, 5xx, timeout) rather than
  returning a structured ERROR response? The attempt is recorded as a distinct failed/retryable
  state, distinguishable from an Opgroeien-side validation ERROR, and the director can retry.
- What happens when the director changes a child's opt-out flag after a month has already been
  successfully submitted? The change takes effect for future periods; it does not retroactively
  alter an already-submitted, immutable submission log entry.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST derive, per location and calendar month, each enrolled child's daily
  presence periods (arrival/departure timestamps) from the existing attendance register (feature
  010), excluding any child with an active toeslag opt-out for that day.
- **FR-002**: System MUST present a director-facing review screen showing the derived payload
  contents (per child, per day, per presence period) before anything is sent to Opgroeien.
- **FR-003**: System MUST let a director submit the reviewed data for a location+month, building
  and sending it as the `POST /opvangprestaties` payload defined in
  `docs/integrations/opgroeien/kinderopvangtoeslag-aaron/KinderOpvangToeslag.json`.
- **FR-004**: System MUST record every submission attempt — successful or not, including
  resubmissions of the same location+period — as a distinct, immutable audit entry containing the
  exact payload sent and the exact response received (or the failure detail, if the call itself
  did not complete).
- **FR-005**: System MUST surface a validation ERROR response's `errorList` entries mapped to the
  specific child and day each error concerns.
- **FR-006**: System MUST let a director resubmit a location+period after correcting underlying
  data, without requiring any prior submission to be deleted or overwritten.
- **FR-007**: System MUST let a director set, view, and end a per-child toeslag opt-out flag with
  an effective period (default start = child's birthdate, open end allowed).
- **FR-008**: System MUST alert a director when a location's care-month submission is missing or
  left in an unresolved-error state as the monthly deadline (the 7th of the following month)
  approaches.
- **FR-009**: System MUST scope all submission data, submission logs, and opt-out flags to the
  owning organisation (tenant) — no cross-tenant visibility.
- **FR-010**: System MUST NOT include an opted-out child, or an opted-out day, anywhere in a
  submitted payload (the AARON contract has no opt-out field — omission is the only mechanism).
- **FR-011**: All director-facing strings for this feature MUST be available via i18n keys
  (NL/FR/EN).
- **FR-012**: System MUST authenticate to the Opgroeien webservice using a bearer token supplied
  per environment via secret/configuration, never hardcoded in source.

### Key Entities *(include if feature involves data)*

- **ToeslagSubmission**: one attempt to submit a location's attendance data for a given calendar
  month to Opgroeien. Holds the exact payload sent, the exact response received (or failure
  detail), a status (success / error / failed-to-send), who submitted it, and when. Immutable
  once created — a correction produces a new `ToeslagSubmission`, not an edit to an existing one.
- **ToeslagOptOut**: a per-child flag recording that the child's presence data must not be sent to
  Opgroeien, with an effective period (start defaults to birthdate, end optional/open).
- **Child** *(extends feature 006)*: this feature requires the child's official first name, last
  name, birthdate, and gender to be present and accurate (Rijksregister-matched), plus an optional
  school-start date — fields the payload contract requires that may not already be captured by
  feature 006's model.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can review and submit a location's monthly attendance data to Opgroeien
  in under 5 minutes, without leaving ChildCare to re-key anything into AARON.
- **SC-002**: 100% of submission attempts (success, error, or failed-to-send) are retrievable
  later with the exact payload and response/failure that occurred, for as long as the record is
  retained.
- **SC-003**: A director can identify, for any given child/day in an ERROR response, which
  specific error applies without cross-referencing a raw API response.
- **SC-004**: A director is alerted to an at-risk (unsubmitted or unresolved-error) location+month
  before the 7th-of-month deadline, with enough lead time to act.
- **SC-005**: An opted-out child's data never appears in a submitted payload, verified across
  every submission for the period the opt-out is active.

## Assumptions

- Feature 010's attendance records provide accurate check-in/check-out timestamps sufficient to
  derive the `checkIn`/`checkOut` values the AARON contract requires — no new attendance-capture
  UI is introduced by this feature.
- The test-environment contract (`KinderOpvangToeslag.json`, host `tstgpappr.kindengezin.be`) is
  representative of the production contract's field shapes; only the base URL and bearer token
  differ between environments. This assumption is safe to build against; it does not require an
  answer to the open production-onboarding question below.
- This feature's design treats every submission attempt (including resubmissions of the same
  location+period) as its own immutable audit record on our side, regardless of how the Opgroeien
  backend itself interprets a repeat `POST` for the same `locatieId`+`periode`. This lets
  implementation proceed without assuming an answer to the open resubmission-semantics question
  below — if Opgroeien's answer later turns out to affect director-facing behavior (e.g. a
  resubmission is rejected as a duplicate rather than accepted), that is a follow-up change to
  the submission flow, not a data-model change.
- Acquiring the actual production bearer token from Opgroeien is an operational/business step
  (contacting software-ontwikkeling@kindengezin.be) taken by the organisation, not a software
  deliverable of this feature — the system only needs the token to be configurable per
  environment.
- "Free-price" (vrije-prijs) eligibility for a location is already determinable from existing
  location/contract data; this feature does not introduce a new location-level pricing-model
  field beyond what already distinguishes IKT (feature 019) locations from free-price ones.

## Open Questions — Requires Product-Owner / Opgroeien Resolution Before `/speckit-plan`

Per `docs/integrations/opgroeien/README.md`'s "do NOT invent" list, these two items are **not**
resolved by this spec and must not be guessed at implementation time:

1. **Production auth/onboarding procedure for the bearer token.** Only the test-environment
   request process (email software-ontwikkeling@kindengezin.be) is documented publicly. This
   spec's design does not depend on the answer (see Assumptions), but the actual production
   rollout cannot happen until this is known.
2. **Correction/resubmission semantics at Opgroeien's end.** Does re-`POST`ing the same
   `locatieId`+`periode` replace the previous submission, get rejected as a duplicate, or
   something else? This spec's design (FR-004/FR-006, immutable per-attempt audit log on our
   side) does not require the answer to proceed to planning, but the director-facing resubmission
   UX may need a follow-up adjustment once Opgroeien confirms the actual server-side behavior.
