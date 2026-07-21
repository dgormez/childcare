# Feature Specification: Digital Contract E-Signature

**Feature Branch**: `024-esignature`

**Created**: 2026-07-21

**Status**: Draft

**Input**: User description: "Allow parents to sign their enrolment contract digitally — no
printing, scanning, or in-person appointment required. Embed a SEPA direct debit mandate in the
same signing flow so payment collection is authorised at the same moment."

## Clarifications

### Session 2026-07-21 (autonomous run — resolved against clear codebase precedent/regulatory
fact, no product-owner ambiguity)

- Q: Does signing become a precondition for a director activating a contract (007's existing
  `Draft → Active` transition), or is it additive alongside the existing lifecycle? → A:
  additive — activation is unchanged. Every downstream feature that depends on `Active` status
  (attendance/BKR — 010, invoicing — 014, occupancy — 012a) already assumes today's
  Draft/Active/Ended semantics; gating that transition on a brand-new capability would be a
  breaking change to four shipped features for a rule the BACKLOG prompt never actually asked
  for. Same additive pattern feature 023 already established (self-registration extended 012a's
  waiting-list lifecycle without changing its status transitions).
- Q: The prompt's "SEPA mandate creditor ID... generated per signing" — is the Creditor
  Identifier (CID) actually generated per signing? → A: no — this conflates two distinct SEPA
  concepts. The **Creditor Identifier** is a single identifier the National Bank of Belgium
  issues per legal entity (the KDV organisation), not something a system generates; it's
  configured once at the organisation level, the same way `Tenant.KboNumber` (feature 014) is
  director-entered once and reused on every invoice. Only the **mandate reference** (a
  per-mandate identifier distinguishing one family's authorisation from another's) is genuinely
  generated per signing, and that part of the original prompt is correct.
- Q: Can a signed contract still be edited in place? → A: no — `UpdateContractCommand` (007)
  already restricts edits to `Draft`-status contracts and revising terms via amendment already
  creates a new `Contract` row (`PreviousContractId`) rather than an in-place edit, specifically
  so a prior version's terms stay intact. A signed contract keeps that same protection: once
  `SignedAt` is set, the contract's terms are frozen, and any revision goes through the existing
  amendment mechanism — which produces a new, not-yet-signed contract requiring its own fresh
  signature. This isn't a new rule; it's applying 007's existing "amendment, not mutation"
  design to the one case it didn't yet cover.

## Product Context

### Feature Type

Mixed — a public, unauthenticated parent-facing signing page (User-facing UI), director-web
additions to send/resend a signing invitation and see signing status (User-facing UI, on a
screen that does not exist yet — see Assumptions), and the backend capability that ties them
together (data-model change on `Contract` and `Tenant`, new API surface, signed-PDF
generation/storage).

### Primary Consumer

Mixed — Parent (the public signing page; a data subject already known to the organisation via
an existing `Contract`/`Child` record, not yet an app user for this flow — same framing as
feature 023's waiting-list parent) and Director (sends/resends invitations, configures the
organisation's SEPA Creditor Identifier, sees signing status).

### Workflow Boundary

Extends the **Child Lifecycle** workflow (`Workflows/child-lifecycle.md`) at the step right
after a waiting-list entry is marked `enrolled` and a contract (007) is created — this feature
is what turns that Draft contract into a signed, legally binding one with an authorised payment
method, without requiring an in-person appointment. `Workflows/child-lifecycle.md` is updated as
part of this spec to describe the new signing step, per `workflows.md`'s governance rules (no
existing business meaning removed — additive to the same lifecycle 007 already defines).

- **Actors**: Parent/Contact (reviews and signs the contract, authorises SEPA collection — no
  account required), Director (sends/resends signing invitations, configures the organisation's
  SEPA Creditor Identifier, sees signing status), System (generates the signing link, sends
  emails, enforces token single-use/expiry, generates and persists the final signed PDF).
- **Actions**: director sends a signing invitation from a Draft contract → parent opens the
  emailed link, reviews the contract terms, signs, and authorises a SEPA mandate with their IBAN
  → system records the signature/mandate, generates and persists the final signed PDF, emails
  both parties, and invalidates the link → director sees the contract's status change to signed.
- **Data Flow**: extends `Contract` (007) with signing-token state, signature capture fields,
  and SEPA mandate fields; extends `Tenant` (001) with a one-time, organisation-level SEPA
  Creditor Identifier (mirrors `KboNumber`, feature 014).
- **Outputs**: a signed, persisted PDF (contract + signature + SEPA mandate) in GCS, a copy
  emailed to parent and director, an updated `Contract` record.
- **Cross-platform Impact**: a new public, unauthenticated web page (director-web's Next.js app,
  new route outside the existing `(app)`/`(auth)` groups, mirrors feature 023's public
  enrollment route); director-web (a minimal contract-signing-status view/action — see
  Assumptions; an organisation-settings addition for the SEPA Creditor Identifier). No
  caregiver-tablet impact. No parent-mobile impact — signing happens on a public web link the
  family may not have the app installed to receive (prompt's own "Out of scope: in-app signing"
  note).

### User Impact

This enables a parent to legally sign their child's enrolment contract and authorise SEPA direct
debit collection remotely, resulting in faster contract completion with no in-person appointment
or manual paperwork for the director.

### UX Requirements

**Persona**: Parent/Guardian (primary, public signing page — a trust-sensitive, one-time
interaction involving a legal document and banking details, not a routine app interaction);
Director (secondary, sends invitations and monitors status from an existing operational
screen).

**Platform**: a new public web page (unauthenticated, one per contract) + director-web
(contract status, org settings). Not the parent mobile app.

**User job (parent)**: "I want to review and sign my child's care contract, and set up
automatic payment, without printing anything or going anywhere in person."

**User job (director)**: "I want to send a contract for signing and know, at a glance, whether
it's been signed yet — without having to phone the family to check."

**Success criteria**: per SC-001–SC-005 below — fast, mobile-browser-friendly signing; reliable
token expiry/single-use enforcement; the signed PDF never silently drifts from what was actually
signed; signing status is glanceable, not a raw field read.

**Main flow (parent)**: emailed link → signing page shows the contract terms in full (child,
location, contracted days, daily rate, photo/video consent) → parent scrolls through, signs
(draw or type) → parent enters IBAN and confirms SEPA authorisation in the same session →
submit → confirmation screen, with a copy of the signed PDF emailed.

**Main flow (director)**: opens a Draft contract → sends a signing invitation → sees status
change to "awaiting signature" → sees status change to "signed" (with date) once the parent
completes it, no polling or manual follow-up needed.

**Loading/empty/error states**: the signing page shows a clear loading state while the contract
loads; an expired, already-used, or tampered link shows one calm, generic "this link is no
longer valid" message — never a raw error, and never any contract/child/family data, since the
requester's identity isn't verified beyond possessing the link; an invalid IBAN is rejected
inline with a specific, human-readable message before submission is allowed.

**Accessibility**: this is a parent-facing form on a public page — per `design-system.md`'s
Forms guidance, no unneeded fields, all inputs have associated labels, validation errors are
`aria-live`-announced rather than color-only, and the signature capture (drawn or typed) must be
usable via keyboard/typed input as a first-class alternative, not just a touch/mouse drawing
pad — a parent on a keyboard-only device must still be able to complete signing.

**Offline behavior**: not applicable — a public web page loaded fresh per visit, same
expectation as feature 023's public enrollment page.

**i18n**: NL/FR/EN throughout (signing page, confirmation, both emails, every new director-facing
label), via this codebase's existing i18n mechanisms.

### Technical Requirements

**API impact**: a new unauthenticated public endpoint group for retrieving contract terms by
signing token and submitting a signature + SEPA mandate (mirrors feature 023's
`RequireTenantExempt()` public-endpoint pattern — tenant resolved from the token itself, not a
JWT claim). Director-facing: a new command to send/resend a signing invitation on a Draft
contract, and an organisation-settings command to set the SEPA Creditor Identifier.

**Data-model impact**:
- `Contract` gains: a signing token and its expiry, `SignedAt`, a signature image, the signer's
  IP, and SEPA mandate fields (IBAN — encrypted at rest, mandate reference, mandate authorised
  timestamp).
- `Tenant` gains a SEPA Creditor Identifier, director-entered once at the organisation level
  (mirrors `KboNumber`).
- An EF Core migration for the above, with a manually run SQL script per this repo's production
  convention (`.claude/CLAUDE.md`).

**Security considerations**: the signing endpoint requires no authentication by design — the
token itself is the credential, single-use, and time-limited; a used, expired, or invalid token
must fail closed with a generic message, matching feature 023's tour-invitation-token pattern.
IBAN is sensitive financial data — encrypted at rest, never logged or returned in full after
capture, mirroring feature 022's National Register Number encryption/display convention. The
final signed PDF, once generated, is the permanent legal record and is never regenerated from
live contract data afterward, even if the underlying contract row is later revised via
amendment.

**Performance considerations**: low-volume, one-signing-per-enrolled-child traffic — no special
performance target beyond the existing API baseline.

**Testing requirements**: token single-use/expiry/tamper rejection; signing produces the correct
`Contract` state and a persisted, non-regenerating PDF; SEPA IBAN validation and encryption at
rest (never plaintext in a response); resending a link invalidates the prior one; revising a
Draft contract after sending invalidates the outstanding link; a signed contract rejects further
edits via the existing update flow; locale-respecting emails and signing-page copy; the
organisation's SEPA Creditor Identifier is required before a signing invitation can be sent (see
Edge Cases).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Parent reviews and digitally signs the contract, authorising SEPA in the same session (Priority: P1)

A parent receives an email with a secure link to sign their child's enrolment contract. They
open it on their phone, no login required, read through the contract terms, sign by drawing or
typing their name, then enter their IBAN and authorise the KDV to collect invoices via direct
debit — all in one sitting. They see a confirmation, and a signed copy arrives by email.

**Why this priority**: This is the entire reason the feature exists — without it, nothing else
in this spec has anything to act on.

**Independent Test**: Can be fully tested by having a director send a signing invitation for a
Draft contract, then completing the public signing flow as the parent and verifying the
contract is marked signed with a persisted PDF and a SEPA mandate recorded.

**Acceptance Scenarios**:

1. **Given** a director has sent a signing invitation, **When** the parent opens the link and
   reviews the contract, **Then** the full contract terms (child, location, contracted days,
   daily rate, photo/video consent) are shown before any signature input is possible.
2. **Given** the parent signs and submits a valid IBAN with SEPA authorisation, **When** the
   submission completes, **Then** the contract is updated with the signature, signer IP, and
   SEPA mandate (with a system-generated mandate reference), the signing token is invalidated,
   a final signed PDF is generated and persisted, and both the parent and the director's
   organisation receive a copy by email.
3. **Given** the parent enters a malformed IBAN, **When** they attempt to submit, **Then** the
   form shows a specific inline error and does not submit.
4. **Given** the parent has already signed and reuses the same link, **When** they open it
   again, **Then** they see a calm "this link is no longer valid" message, not the contract or
   a way to sign again.

---

### User Story 2 - Director sends a contract for signing and sees its status (Priority: P1)

A director opens a Draft contract and sends a signing invitation. The screen shows the contract
is now "awaiting signature." Once the parent completes signing (User Story 1), the director sees
the status update to "signed," with the date, and can access the signed PDF — without needing to
phone the family to check or manually chase a paper copy.

**Why this priority**: Without this, User Story 1 has no way to start, and a director has no way
to know a contract has been completed — the director-side half of the loop is what makes the
digital flow actually replace the in-person one.

**Independent Test**: Can be fully tested by sending an invitation from a Draft contract,
verifying the status shows "awaiting signature," completing signing (User Story 1), and
verifying the status updates to "signed" with a working link to the stored PDF, with zero
retyping of any contract data.

**Acceptance Scenarios**:

1. **Given** a Draft contract with a contact email on file, **When** the director sends a
   signing invitation, **Then** an email is sent to the contact containing the signing link, and
   the contract's status is visibly "awaiting signature."
2. **Given** a Draft contract with no contact email on file, **When** the director attempts to
   send a signing invitation, **Then** the action is rejected with a clear message, not a silent
   failure.
3. **Given** a contract has been signed, **When** the director views it, **Then** the signed
   date is shown and the signed PDF is accessible.

---

### User Story 3 - Signing links stay correct over time: resend on expiry, invalidate on revision (Priority: P2)

A signing link expires before the parent gets to it, so the director sends a new one — the old
link stops working the moment the new one is issued. Separately, a director realises a contract
needs a change after sending it for signing but before the parent has signed; editing the
contract immediately invalidates the outstanding link, so a parent can never sign stale terms.

**Why this priority**: The core loop (User Stories 1–2) works correctly the first time through,
but real usage will hit expired links and last-minute contract corrections — without this, a
stale link either wastes the parent's time or, worse, lets them sign terms the director already
changed their mind about.

**Independent Test**: Can be fully tested by letting a link expire (or manually invalidating it)
and verifying a resend issues a working new link while the old one stays dead; separately, by
editing a contract after sending an invitation and verifying the previously sent link no longer
works.

**Acceptance Scenarios**:

1. **Given** a signing link has expired, **When** the parent opens it, **Then** they see a calm
   "this link is no longer valid" message, and the director can resend a new one.
2. **Given** a director resends a signing invitation, **When** the new email is sent, **Then**
   any previously issued, unsigned link for that contract stops working immediately.
3. **Given** a director edits a Draft contract that already has an outstanding signing
   invitation, **When** the edit is saved, **Then** the outstanding link is invalidated and the
   director must send a new invitation before the parent can sign.

---

### User Story 4 - Director configures the organisation's SEPA Creditor Identifier (Priority: P2)

Before any contract's SEPA mandate is legally valid, the organisation's own SEPA Creditor
Identifier (issued by the National Bank of Belgium) needs to be on file. A director enters it
once in organisation settings, the same way the company registration number is already set up.

**Why this priority**: This is a one-time setup step, not a per-signing action — it needs to
happen before the first real mandate is captured, but doesn't need to ship alongside User
Stories 1–2 to be independently useful, and a test environment can seed it directly.

**Independent Test**: Can be fully tested by setting the Creditor Identifier in organisation
settings and verifying it appears correctly on the next contract's signed PDF and SEPA mandate
data — independent of the signing flow itself.

**Acceptance Scenarios**:

1. **Given** an organisation has not yet set a SEPA Creditor Identifier, **When** a director
   attempts to send a signing invitation, **Then** the action is rejected with a clear message
   directing them to organisation settings, rather than allowing an invalid mandate to be
   captured.
2. **Given** a director sets the SEPA Creditor Identifier in organisation settings, **When** they
   save it, **Then** it is used on every subsequently generated signed PDF and SEPA mandate for
   that organisation.

---

### Edge Cases

- A parent starts signing but abandons the page before submitting — no partial signature or
  mandate is recorded; the link remains valid until it expires or the director resends.
- A parent opens the signing link on a mobile browser, not the Expo/parent-mobile app — the
  signing page is a responsive web page, not an app screen (prompt's own explicit constraint).
- A director attempts to send a signing invitation before the organisation's SEPA Creditor
  Identifier is configured — rejected up front (User Story 4), never allowing a mandate to be
  captured against a missing creditor identity.
- A director tries to edit a contract that has already been signed — rejected; any revision
  must go through the existing amendment mechanism (007), which creates a new, unsigned contract
  requiring its own fresh signature.
- A tampered or malformed token is submitted directly against the signing endpoint (bypassing
  the UI) — rejected server-side with the same generic message the UI shows for an expired link,
  never revealing which failure mode occurred.
- A parent's IBAN belongs to a non-Belgian SEPA country — accepted, since SEPA direct debit is
  not Belgium-only; only the IBAN's own checksum/format is validated, not its country.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A director MUST be able to send a signing invitation from a Draft-status contract
  that has a contact email on file; a contract with no contact email MUST be rejected with a
  clear message rather than silently failing.
- **FR-002**: The signing invitation email MUST contain a secure, single-use, time-limited link
  requiring no login or account to use.
- **FR-003**: A signing link MUST expire 72 hours after being issued; an expired link MUST be
  rejected with a calm, generic message, never contract data.
- **FR-004**: A director MUST be able to resend a signing invitation for a contract that has not
  yet been signed; resending MUST immediately invalidate any previously issued, unsigned link
  for that contract.
- **FR-005**: The public signing page MUST require no login and MUST display the full contract
  terms (child name, location, contracted days, daily rate, photo/video consent) before any
  signature input is accepted.
- **FR-006**: A parent MUST be able to provide a signature either by drawing (touch/mouse) or by
  typing their name, and MUST explicitly confirm intent to sign before submission.
- **FR-007**: The same signing session MUST also capture a SEPA direct debit mandate: the
  parent's IBAN and an explicit authorisation for the organisation to collect invoices via
  direct debit.
- **FR-008**: The system MUST validate the submitted IBAN's format/checksum before accepting the
  mandate, rejecting an invalid one with a specific inline error.
- **FR-009**: On successful signing, the system MUST record the signed timestamp, the signature
  (image or typed text), the signer's IP address, and the SEPA mandate (IBAN encrypted at rest,
  a system-generated unique mandate reference, and the authorisation timestamp), and MUST
  invalidate the signing token so it cannot be used again.
- **FR-010**: On successful signing, the system MUST generate a final signed PDF (contract terms
  + signature + SEPA mandate details) and persist it; this stored PDF MUST NOT be regenerated
  from live contract data at any later point, even if the underlying contract is later amended.
- **FR-011**: On successful signing, the system MUST send a copy of (or link to) the signed PDF
  to both the parent and the organisation's director(s).
- **FR-012**: A signing link that has already been used, has expired, or fails validation for
  any reason MUST show one calm, generic "no longer valid" message and MUST NOT reveal any
  contract, child, or family data.
- **FR-013**: If a director edits a Draft contract that has an outstanding (unsigned, unexpired)
  signing invitation, the system MUST invalidate that outstanding link; the director MUST send a
  new invitation before the parent can sign the revised terms.
- **FR-014**: A contract with `SignedAt` set MUST reject further edits via the existing contract
  update flow (007); any revision after signing MUST go through the existing amendment
  mechanism, producing a new, unsigned contract that requires its own fresh signature.
- **FR-015**: Signing a contract MUST NOT be a precondition for transitioning that contract from
  `Draft` to `Active` (007's existing transition is unchanged).
- **FR-016**: The organisation's SEPA Creditor Identifier MUST be configurable once at the
  organisation level by a director, and MUST be required (with a clear rejection if missing)
  before a signing invitation can be sent.
- **FR-017**: Each signed contract MUST receive its own unique, system-generated SEPA mandate
  reference distinct from the organisation's Creditor Identifier.
- **FR-018**: Director-web MUST show, for a given contract, whether it has not been sent, is
  awaiting signature, or has been signed (with the signed date) — legible at a glance, not
  requiring the director to read a raw timestamp field.
- **FR-019**: All user-facing text on the signing page, both emails, and every new
  director-facing label MUST be available in Dutch, French, and English via the existing i18n
  mechanism; the signing page's language MUST default to the parent's known locale preference if
  available, otherwise a sensible fallback, and MUST offer a manual toggle.
- **FR-020**: IBAN MUST be encrypted at rest, MUST NOT be logged in plaintext, and MUST NOT be
  returned in full in any API response after capture (masked, e.g. last 4 digits only).
- **FR-021**: The public signing and mandate-submission endpoints MUST NOT require or accept any
  authentication credential, and MUST NOT expose any data beyond the single contract the
  requester's token resolves to.

### Key Entities

- **Contract** (existing, feature 007, extended): gains signing-token state (token, expiry),
  signature capture (signed timestamp, signature image/text, signer IP), and SEPA mandate data
  (encrypted IBAN, system-generated mandate reference, mandate authorisation timestamp) — all as
  fields on the existing entity, not a separate history log (mirrors feature 023's precedent
  that this codebase has no per-change history table pattern).
- **Tenant** (existing, feature 001, extended): gains a SEPA Creditor Identifier, director-entered
  once at the organisation level (mirrors the existing `KboNumber` field, feature 014).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A parent can review and complete signing, including the SEPA mandate, in under 5
  minutes on a typical mobile browser.
- **SC-002**: 100% of signing links older than 72 hours are rejected, and a director can issue a
  replacement link in under 1 minute.
- **SC-003**: The signed PDF retrieved for a contract is always exactly what was generated at
  the moment of signing — zero instances of it reflecting contract data changed afterward.
- **SC-004**: A director can determine a contract's signing status (not sent / awaiting
  signature / signed) without opening or interpreting any raw data field.
- **SC-005**: 100% of signing attempts using a reused, expired, or tampered token are rejected
  server-side, even when the request bypasses the UI entirely.

## Assumptions

- Contracts (007) currently have no dedicated director-web screen at all
  (`web/app/(app)/contracts/page.tsx` is a stub). This feature builds the minimal
  contract-signing view/actions it actually needs (send/resend invitation, signing status, link
  to the signed PDF) — not a full contract creation/management UI, which stays out of scope
  here, matching the precedent set by features 007a/013c/023 of building only the screen surface
  a feature genuinely requires.
- The parent's "known locale preference" (FR-019) comes from the `Contact` record associated
  with the contract's child, if one exists and has a locale set; otherwise the signing page
  defaults to Dutch, matching this codebase's existing default-locale convention (e.g. feature
  023's `DefaultEnrollmentLocale` default).
- The signature captured is a simple/advanced electronic signature (drawn or typed image plus
  signer IP and timestamp as evidentiary metadata) — equivalent to industry-standard e-signature
  tools for this class of document, not a qualified electronic signature (eIDAS Level 2+), per
  the feature's own explicit "Out of scope" note.
- SEPA direct debit *collection* (the actual batch XML generation and bank submission) is a
  separate feature (026) — this feature only captures and stores a valid, authorised mandate;
  it does not submit anything to a bank.
- In-app signing via the parent mobile app is out of scope (Phase 3, per the feature's own
  explicit exclusion) — the signing page is reached only via the emailed link.
