# Feature Specification: SEPA Direct Debit Batch Collection

**Feature Branch**: `026-sepa-direct-debit`

**Created**: 2026-07-22

**Status**: Draft

**Input**: User description: "Generate SEPA direct debit XML (pain.008.001.02 format) so the KDV can collect invoice amounts directly from parent bank accounts in a single batch, rather than waiting for individual transfers."

## Product Context

### Feature Type

API-backend capability (pain.008 XML generation, batch lifecycle) with a Mixed UI layer — a
new director-web screen to select invoices, review eligibility, set an execution date, and
download the batch.

### Primary Consumer

Director (selects invoices, generates and downloads a batch, handles a returned debit, revokes a
mandate on request); System (marks a batch's invoices `pending_debit` on generation, and — via
feature 025's existing CODA reconciliation — `paid` once the bank's statement confirms
collection).

### Workflow Boundary

Extends the **Billing & Payments** workflow (`Workflows/billing.md`) — this feature adds SEPA
direct debit as a second collection-initiation method alongside the existing bank-transfer/OGM
flow (feature 014) and plugs directly into feature 025's existing CODA-driven mark-paid
mechanism, rather than introducing a new one. `Workflows/billing.md` is updated as part of this
spec to describe the batch-generation and return-handling steps, per `workflows.md`'s governance
rules (additive to the existing financial relationship it already governs — nothing removed).

- **Actors**: Director (generates batches, downloads the XML, marks a returned debit, revokes a
  mandate), Parent (no action in this feature — already signed a mandate via feature 024;
  revocation is director-initiated on the parent's behalf, consistent with this codebase having
  no parent self-service billing action anywhere — see `Workflows/billing.md`'s existing
  Actors list), System (validates eligibility, generates and schema-validates the pain.008 XML,
  transitions invoice status, and — via 025 — reconciles a confirmed collection).
- **Actions**: director selects a set of `sent` invoices for a location and month → system shows
  which are eligible (signed, non-revoked mandate) and which are excluded (with a reason) →
  director sets an execution date → system generates a schema-validated pain.008 file and marks
  the included invoices `pending_debit` → director downloads the file and uploads it to their
  bank's portal (outside this system) → when the bank's CODA statement (025) later confirms the
  collection, the invoice is marked `paid` automatically, identically to a manual bank-transfer
  match → if the bank instead returns a debit (R-transaction), the director marks the affected
  invoice back to `sent` with a note, for normal follow-up.
- **Data Flow**: reads `Contract` (024's mandate fields: `SepaIbanEncrypted`,
  `SepaMandateReference`, `SepaAuthorisedAt`, plus this feature's new `SepaRevokedAt`), `Invoice`
  (014's `OgmReference`, `TotalCents`, `Status`), `Tenant.SepaCreditorIdentifier` (024) and
  `Location.BankAccountNumber` (014) as the pain.008 creditor headers — no new settings storage;
  writes a new `SepaBatch` record (audit/history) and an `Invoice.SepaBatchId` back-reference,
  and transitions `Invoice.Status` between `Sent`, `PendingDebit`, and `Paid`.
- **Outputs**: a downloaded pain.008.001.02 XML file; an audit record of every generated batch
  (who, when, which invoices, execution date); updated invoice statuses.
- **Cross-platform Impact**: director-web only. No caregiver-tablet or parent-mobile surface —
  parents already interact with SEPA exclusively through feature 024's mandate-signing page; this
  feature adds no new parent-facing screen or notification (pre-notification email is explicitly
  out of scope per the originating backlog description, deferred to feature 020).

### User Impact

This enables a Director to collect a batch of parent invoice payments automatically via SEPA
direct debit rather than waiting on manual bank transfers, resulting in faster, more predictable
cash collection with less month-end payment-chasing.

### UX Requirements

- **Persona**: Director — the only persona with any surface in this feature.
- **Platform**: Director web, desktop-first, high information density per `platform-rules.md`'s
  Director Web section (`platform-rules.md`).
- **User job**: "Generate this month's SEPA batch for a location and get a clean pain.008 file I
  can upload to my bank."
- **Success criteria**: a director can go from "invoices are sent" to "batch downloaded and ready
  to upload" in a handful of clicks, with zero ineligible invoices ever silently included.
- **Main flow**: Billing → SEPA Batches → select location + month → review eligible/excluded
  invoice list → set execution date → Generate → download.
- **Loading/empty/error states**: no eligible invoices for the selected location/month (empty
  state, not an error); an invoice excluded for a missing/revoked mandate (shown with its reason
  inline, not hidden); an execution date before the minimum allowed business day (inline
  validation, no server round-trip needed to catch it); a schema-validation failure on the
  generated XML (a generic, human-readable error — see FR-011 — never a raw XSD validator
  message, per this codebase's standing error-handling convention).
- **Accessibility**: full keyboard operability for the selection list, date picker, and
  Generate action, with a visible focus ring throughout, per `platform-rules.md`'s Director Web
  section.
- **Offline behavior**: not applicable — director web has no offline mode anywhere in this
  codebase (offline queuing is a caregiver-tablet-only concern, feature 008), and nothing about
  this feature introduces a first exception to that.

### Technical Requirements

- **API impact**: new endpoints to list batch-eligible invoices for a location/month, generate a
  batch (returns the XML for download), list prior batches, and mark a `pending_debit` invoice
  returned; a new director action on a contract to revoke its SEPA mandate.
- **Data-model impact**: `Contract.SepaRevokedAt` (new); `InvoiceStatus.PendingDebit` (new,
  inserted between `Sent` and `Paid`); a new `SepaBatch` entity plus `Invoice.SepaBatchId`. No new
  settings fields — the creditor identifier (`Tenant.SepaCreditorIdentifier`, feature 024) and
  creditor IBAN (`Location.BankAccountNumber`, feature 014) already exist; see Assumptions.
- **Security considerations**: the debtor IBAN decrypted for XML generation is the same data
  feature 024 already protects via `IIbanProtector` — reuse that purpose string (it is the same
  IBAN, not a distinct data class, unlike feature 025's separate CODA-sender-IBAN purpose); the
  generated XML (containing IBANs) is held only in memory for the download response, never
  persisted to disk/storage.
- **Performance considerations**: batch generation and schema validation for a typical
  single-location monthly batch (tens to low hundreds of invoices) must complete synchronously
  within a single request.
- **Testing requirements**: pain.008 XML structural/schema validity; eligibility filtering
  (signed vs. missing vs. revoked mandate); execution-date minimum-business-day validation;
  invoice status transitions (`Sent → PendingDebit`, `PendingDebit → Paid` via 025,
  `PendingDebit → Sent` via a returned debit); an invoice can never appear in two batches while
  `pending_debit`.

## Clarifications

### Session 2026-07-22

- Q: The originating backlog description says to store the creditor identifier (CID), creditor
  name, and creditor IBAN as new location-level settings — but feature 024 already added
  `Tenant.SepaCreditorIdentifier` (an organisation-wide CID, mirroring `KboNumber`) and feature
  014 already added `Location.BankAccountNumber` (the location's own bank account, already used
  as the "pay to this account" IBAN on invoice PDFs). Should this feature add a second,
  duplicate settings surface, or reuse what already exists? → A: Reuse the existing fields — no
  new settings entity. The CID is issued once per legal entity (Tenant-level, consistent with
  024's design), the creditor IBAN is the same per-location account already printed on invoices
  (Location-level, consistent with 014's design), and the creditor name is the organisation's
  registered name (`Tenant.Name`, already exists). Resolved directly against the existing data
  model rather than guessed, per this pipeline's standing rule for a BACKLOG premise that
  research shows is factually wrong before planning continues.
- Q: The SEPA CORE scheme requires each debit instruction to carry a sequence type — `FRST` for
  the first-ever collection under a given mandate, `RCUR` for every collection after — a
  business rule the pain.008 XSD itself does not enforce, so schema-validity alone (FR-006) would
  not catch getting this wrong. How does the system determine which one applies? → A: `FRST` if
  no earlier batch has ever successfully included an invoice under the contract's *current* SEPA
  mandate reference (a mandate reference changes on revoke-and-resign, so a re-signed mandate
  correctly starts at `FRST` again); `RCUR` otherwise. This is the standard SEPA CORE rule, not a
  product decision — resolved as a recommended default per this pipeline's standing rule, not
  raised to the product owner.
- Q: How does the system determine the minimum allowed execution date ("at least 1 business day
  in the future")? Does "business day" mean the location's own closure calendar (feature 011), a
  full Belgian-bank-holiday calendar, or a simple weekday rule? → A: A simple Monday–Friday
  weekday rule, independent of feature 011's KDV closure calendar (a closure day is about whether
  the *center* is open, not whether the *banking system* processes SEPA files) and independent of
  a full Belgian public-holiday calendar (no such calendar exists anywhere in this codebase yet,
  and introducing one is disproportionate to this feature's scope). Consistent with this
  codebase's existing precedent of treating "business day" as Monday–Friday only (feature 024's
  `CreateContractCommandValidator`) rather than inventing a second definition.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate and download a SEPA batch (Priority: P1)

A director has a month's worth of sent invoices for a location. Most families have a signed SEPA
mandate on file. The director opens the SEPA Batches screen, picks the location and month, sees
which invoices are eligible, sets an execution date, and generates a pain.008 XML file ready to
upload to their bank's portal.

**Why this priority**: This is the entire value proposition of the feature — without it, nothing
ships. Everything else (auto-reconciliation, returns, revocation) only matters once a batch can
actually be produced.

**Independent Test**: Create several `sent` invoices for a location, some for parents with a
signed, non-revoked SEPA mandate and some without. Generate a batch for that location/month with
a valid execution date. Verify the downloaded file is a well-formed, schema-valid pain.008.001.02
document containing exactly one debit instruction per eligible invoice (correct amount, debtor
IBAN, mandate reference, mandate signing date, and end-to-end ID equal to the invoice's OGM
reference), and that each included invoice is now `pending_debit`.

**Acceptance Scenarios**:

1. **Given** a location with a configured SEPA creditor identifier and bank account, and three
   `sent` invoices whose families each have a signed, non-revoked mandate, **When** the director
   generates a batch for that location covering those invoices with a valid execution date,
   **Then** the system produces a pain.008.001.02 file with three debit instructions and all
   three invoices become `pending_debit`.
2. **Given** a location with no SEPA creditor identifier configured yet, **When** the director
   opens the SEPA Batches screen for that location, **Then** they see a clear message to
   configure the organisation's creditor identifier first (linking to where feature 024 already
   exposes it), rather than a batch that silently omits the required header.
3. **Given** a batch has just been generated, **When** the director views the batch history,
   **Then** they see that batch listed with its execution date, invoice count, and total amount.
4. **Given** an execution date less than one business day in the future, **When** the director
   attempts to generate a batch with it, **Then** generation is rejected before any invoice
   status changes, with the minimum valid date shown.
5. **Given** a set of selected invoices whose generated XML would fail schema validation (e.g., a
   malformed IBAN slipping past earlier checks), **When** generation runs, **Then** the system
   rejects the download with a clear, human-readable error and no invoice status changes — the
   file is never handed to the director invalid, and never partially applied.

---

### User Story 2 - Collection confirmed automatically via bank statement (Priority: P2)

A few days after uploading a batch to their bank, the director imports the bank's CODA statement
(feature 025) as they already do for regular transfers. The invoices that were part of the SEPA
batch and successfully collected are marked paid automatically, with no separate manual step for
SEPA versus a regular transfer.

**Why this priority**: Without this, generating a batch still leaves the director manually
marking dozens of invoices paid one by one — most of the "less payment-chasing overhead" value
promised to the director depends on this closing automatically, the same way a regular
bank-transfer payment already does via 025.

**Independent Test**: Generate a batch containing one invoice (now `pending_debit`). Import a
CODA statement whose transaction carries that invoice's OGM reference and a matching amount.
Verify the invoice is marked `paid` exactly as an ordinary bank-transfer match would be, and that
it is treated as "open" for 025's amount+IBAN suggested-match path the same way a `sent` invoice
already is.

**Acceptance Scenarios**:

1. **Given** an invoice in `pending_debit` from a generated batch, **When** a CODA import (025)
   contains a transaction whose reference exactly matches that invoice, **Then** the invoice is
   marked `paid` with the transaction's value date and amount, identically to feature 025's
   existing exact-match behavior for a `sent` invoice.
2. **Given** an invoice in `pending_debit`, **When** no matching CODA transaction has been
   imported yet, **Then** the invoice remains visible as `pending_debit` (not `sent`, not `paid`)
   in both the invoice list and the SEPA batch history.
3. **Given** an invoice in `pending_debit` with no exact reference match, **When** a CODA
   transaction's amount and sender IBAN match it, **Then** it is offered through 025's existing
   suggested-match review, the same way a `sent` invoice already is.

---

### User Story 3 - Handle a returned debit (Priority: P2)

A debit in a submitted batch fails at the bank (insufficient funds, closed account, mandate
disputed) and comes back as an R-transaction. The director needs to see which invoice that was
and get it back into the normal follow-up flow rather than have it silently stuck as
`pending_debit` forever.

**Why this priority**: Without this, a returned debit leaves an invoice permanently stranded in
`pending_debit` — invisible to the director's normal `sent`/overdue follow-up — which is worse
than not automating collection at all, since the family would never get chased for a payment
that never actually arrived.

**Independent Test**: Put an invoice into `pending_debit` via a generated batch. Mark it as
returned with a reason. Verify it reverts to `sent` (eligible for normal overdue tracking and a
future batch) and the reason is visible on the invoice.

**Acceptance Scenarios**:

1. **Given** an invoice in `pending_debit`, **When** the director marks it as returned and enters
   a reason (e.g., "insufficient funds"), **Then** the invoice reverts to `sent` with that reason
   visible on it, and it becomes eligible for a future batch or manual payment recording again.
2. **Given** an invoice already marked `paid` via 025's reconciliation, **When** the director
   looks for a "mark returned" action, **Then** it is not offered — a paid invoice is immutable,
   consistent with `Workflows/billing.md`'s existing rule, and a genuine post-payment reversal is
   out of this feature's scope (see Assumptions).
3. **Given** a returned invoice back in `sent` status, **When** the director includes it in a
   later batch, **Then** it is treated exactly like any other eligible `sent` invoice — no special
   "previously returned" restriction blocks it.

---

### User Story 4 - Exclude invoices without a valid mandate, and revoke one on request (Priority: P3)

Not every family has signed a SEPA mandate, and a family that has can ask to stop future direct
debits (e.g., they closed that bank account). The director needs those invoices left out of every
batch automatically, and a way to revoke a mandate on a family's request so it's never
accidentally included later.

**Why this priority**: Lower priority than P1/P2 because the core batch-generation flow already
requires a signed, non-revoked mandate to include an invoice (P1's own eligibility check) — this
story is about giving the director an explicit way to *create* the revoked state and see why an
invoice was excluded, not about the exclusion rule itself, which P1 already covers structurally.

**Independent Test**: Revoke a family's mandate. Generate a batch covering an invoice for that
family. Verify the invoice is excluded with a "mandate revoked" reason, and that a `sent` invoice
for a family with no mandate at all is excluded with a distinct "no mandate" reason.

**Acceptance Scenarios**:

1. **Given** a contract with a signed, non-revoked SEPA mandate, **When** the director revokes
   it, **Then** the contract is no longer eligible for any future batch, and its record shows the
   mandate as revoked.
2. **Given** a `sent` invoice whose family has never signed a SEPA mandate, **When** the director
   reviews batch-eligible invoices, **Then** it appears in the excluded list with a "no mandate on
   file" reason, distinct from a revoked-mandate exclusion.
3. **Given** a family whose IBAN has changed since their mandate was signed, **When** the director
   wants to update it, **Then** there is no direct edit of the IBAN on an existing mandate — the
   director revokes the old mandate and sends a new signing invitation (reusing feature 024's
   existing invitation flow) so the family re-authorises under the new account, never a silent
   override of a signed mandate's IBAN.

---

### Edge Cases

- What happens when a director tries to include the same invoice in two batches at once (e.g., two
  browser tabs)? The second attempt fails the eligibility check, since the invoice is already
  `pending_debit` by the time the second batch is generated — only `sent` invoices are eligible.
- What happens when a location has a creditor IBAN but no creditor identifier configured (or vice
  versa)? Batch generation is blocked with a message naming exactly which configuration is
  missing, rather than generating a pain.008 file with a blank required header.
- What happens when an invoice's amount is zero or negative (should not occur given feature 014's
  billing rules, but the check exists as a defensive boundary)? It is excluded from eligibility
  with a reason, never included as a zero/negative debit instruction.
- How does the system treat a mandate signed today, when a batch is generated with an execution
  date far in the future? It is eligible — a mandate has no separate "first-use" cooling-off
  period modeled in this system beyond the standard one-business-day execution-date minimum
  (see Assumptions).
- What happens if a batch's location has zero eligible invoices for the selected month? The
  director sees an explanatory empty state, not an error, and is not able to generate an empty
  batch.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Directors MUST be able to view, for a chosen location and month, every `sent`
  invoice split into eligible (signed, non-revoked SEPA mandate) and excluded (with a specific
  reason: no mandate, revoked mandate, or non-positive amount) groups.
- **FR-002**: Directors MUST be able to select some or all eligible invoices, set an execution
  date, and generate a pain.008.001.02 XML batch containing one debit instruction per selected
  invoice: amount, debtor IBAN (decrypted from the contract's mandate), debtor name, mandate
  reference, mandate signing date, sequence type, and end-to-end ID equal to the invoice's
  existing OGM reference.
- **FR-002a**: Each debit instruction's sequence type MUST be `FRST` if no earlier batch has ever
  successfully included an invoice under that contract's current SEPA mandate reference, and
  `RCUR` otherwise — a mandate reference issued after a revoke-and-resign (FR-011/FR-012) MUST
  correctly restart at `FRST`.
- **FR-003**: The generated batch's required creditor headers (creditor identifier, creditor
  name, creditor IBAN) MUST come from the location's organisation-level SEPA creditor identifier
  (feature 024) and the location's existing bank account (feature 014) and name — the system MUST
  NOT introduce a second, separately-maintained copy of any of these values.
- **FR-004**: The system MUST reject batch generation before any invoice status changes if the
  selected location is missing its creditor identifier or its bank account, naming which one is
  missing.
- **FR-005**: The execution date MUST be at least one business day (Monday–Friday) after the date
  of generation. The system MUST reject an earlier date before generating anything, and MUST show
  the director the minimum valid date.
- **FR-006**: The system MUST validate the generated XML against the official pain.008.001.02
  schema before it is offered for download. A file that fails validation MUST NOT be returned to
  the director, MUST NOT change any invoice's status, and MUST surface a clear, human-readable
  error rather than raw schema-validator output.
- **FR-007**: On successful generation, every included invoice MUST transition from `Sent` to a
  new `PendingDebit` status, and the batch itself MUST be persisted (execution date, location,
  generated-by, generated-at, and its set of invoices) for later reference.
- **FR-008**: Directors MUST be able to view a history of previously generated batches for a
  location, each showing its execution date, invoice count, and total amount.
- **FR-009**: A `PendingDebit` invoice MUST be treated as an open/matchable invoice by feature
  025's existing CODA reconciliation (exact-reference match and amount+IBAN suggested match)
  exactly as a `Sent` invoice already is, and MUST transition to `Paid` the same way.
- **FR-010**: Directors MUST be able to mark a `PendingDebit` invoice as returned, with a required
  reason. This transitions the invoice back to `Sent` (visible for normal overdue follow-up and
  eligible for a future batch) and records the reason on the invoice. This action MUST NOT be
  available on a `Paid` invoice.
- **FR-011**: Directors MUST be able to revoke a contract's SEPA mandate. A revoked mandate MUST
  make every current and future invoice for that contract ineligible for any batch (FR-001's
  eligibility check) until a new mandate is signed. Revoking MUST NOT delete the mandate's
  history (signing date, prior mandate reference) — see FR-012.
- **FR-012**: The system MUST NOT allow directly editing the IBAN on an existing signed mandate.
  Correcting a changed IBAN MUST go through revoking the current mandate (FR-011) and sending a
  new signing invitation via feature 024's existing invitation flow.
- **FR-013**: An invoice MUST never appear as eligible in more than one batch at a time — once
  `PendingDebit`, it is excluded from every subsequent batch's eligible list until it returns to
  `Sent` (FR-010) or becomes `Paid` (FR-009).
- **FR-014**: The debtor IBAN decrypted during batch generation MUST use the same IBAN-protection
  mechanism and purpose string feature 024 already established for a contract's mandate IBAN, and
  each decryption for XML generation MUST be logged as an access event, consistent with this
  system's existing handling of other financial PII.
- **FR-015**: All director-facing strings introduced by this feature MUST be available in Dutch,
  French, and English.

### Key Entities

- **SEPA Batch**: A single generated pain.008 export — location, execution date, generated-by
  director, generated-at timestamp, and the set of invoices it included (for audit/history and
  FR-008's batch list). Immutable once created; a returned debit (FR-010) changes the affected
  invoice, not the batch record itself.
- **Invoice** *(existing, feature 014)*: Gains a new `PendingDebit` status between `Sent` and
  `Paid`, and a reference to the batch that put it there.
- **Contract** *(existing, feature 024)*: Gains a revoked-at marker on its SEPA mandate fields,
  distinguishing "never signed" from "signed then revoked" for FR-001's exclusion reasons.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can go from a month's sent invoices to a downloaded, bank-ready pain.008
  file in a handful of clicks, without leaving the director web app.
- **SC-002**: Zero invoices without a currently-valid (signed, non-revoked) SEPA mandate are ever
  included in a generated batch.
- **SC-003**: Zero generated batches are ever offered for download in a schema-invalid state.
- **SC-004**: An invoice successfully collected via SEPA reaches `Paid` status through the exact
  same CODA-import step a director already uses for regular bank-transfer payments — no separate
  reconciliation action for SEPA-collected invoices.
- **SC-005**: A returned debit never leaves an invoice permanently stuck outside the director's
  normal sent/overdue view.

## Assumptions

- No new settings entity is introduced for creditor identifier/name/IBAN — this feature reuses
  `Tenant.SepaCreditorIdentifier` (024), `Tenant.Name`, and `Location.BankAccountNumber` (014).
  See Clarifications.
- "Business day" for the execution-date minimum (FR-005) means Monday–Friday, independent of the
  location's closure calendar (011) and independent of any Belgian public-holiday calendar (none
  exists in this codebase). See Clarifications.
- R-transaction (returned debit) detection is director-initiated (FR-010), not automatically
  parsed from a CODA statement's return codes — the originating backlog description frames
  returns as something "the director sees," and no existing CODA-import code (025) currently
  distinguishes a return transaction type from a payment; automatic R-transaction parsing can be
  a future enhancement to 025 if it proves to be a real gap once this ships.
- A parent has no in-app self-service action for their own SEPA mandate anywhere in this
  codebase (signing is a one-time public link, feature 024) — revocation is director-initiated
  on the family's behalf (FR-011), consistent with every other billing action in
  `Workflows/billing.md` being director-only.
- The B2B SEPA scheme and pre-notification emails to parents are out of scope, per the originating
  backlog description; pre-notification is deferred to feature 020 when this feature ships.
- The official EPC pain.008.001.02 XSD is a published, standard schema (not proprietary or
  tenant-specific) — sourced during implementation the same way this codebase already sources
  other standard technical schemas, not a product/business decision requiring the product owner.
- A genuine post-payment reversal (a `Paid` invoice whose collection is later reversed by the
  bank) is out of scope — feature 014's existing rule that a paid invoice is immutable holds
  here too; a credit-note/adjustment mechanism for that case remains a known future gap, same as
  `Workflows/billing.md` already notes for a correction after payment.
