# Feature Specification: Fiscal Attestations

**Feature Branch**: `015-fiscal-attestations`

**Created**: 2026-07-16

**Status**: Draft

**Input**: User description: "Build annual fiscal attestations for parents to claim childcare
costs on their Belgian tax return. Per-child, per-tax-year PDF (QuestPDF), computed from actually
paid invoices (feature 014), up to 4 rate periods, NRN never stored, bulk generation at year-end,
director can regenerate/correct. MVP is PDF only — directors file with FOD Financiën via
Belcotax-on-web manually; automated electronic filing is feature 019 item 5."

## Product Context

### Feature Type

Mixed (Data-model change + API-backend capability, with a director-web UI to trigger/manage
generation and a parent-mobile read/download extension).

### Primary Consumer

Director (bulk-generates and corrects attestations). Parent (downloads the PDF as the ultimate
beneficiary; does not initiate anything in this MVP).

### Workflow Boundary

**Government Reporting & Compliance** (`workflows.md` / `Workflows/government-reporting.md`) —
explicitly listed there: "Fiscal attest 281.86 generation and Belcotax-on-web filing (features
015, 019)."

Actors: Director (bulk-generates for a tax year, regenerates/corrects an individual attestation).
System (aggregates paid invoices per child/location/tax year into rate periods, renders the PDF).
Parent (views/downloads their child's attestation).

Actions: Director opens the Fiscal Attestations area for a chosen tax year → system determines
every child with at least one `Paid` invoice in that year → director triggers bulk generation →
system aggregates each eligible child's paid invoices into up to 4 contiguous rate periods per
location and renders one PDF per (child, tax year, location) → director sees per-child
generation status → director can regenerate a single attestation later (e.g. a parent reports an
error) → system re-aggregates current paid-invoice data and replaces the existing PDF. Parent
opens their child's attestation from the parent app and downloads it.

Data Flow: `Invoice` (014, `Paid` status only) for a tax year → grouped by child + location →
aggregated into `line_items` (period start/end, day count, amount, daily rate) → QuestPDF render
→ stored PDF, referenced by the attestation record. Outputs: one PDF per (child, tax year,
location), listed in director-web with a status per child; downloadable by the linked parent
contact(s) in parent-mobile.

Cross-Platform Impact: Director web (primary — bulk generation, per-child status, regenerate).
Parent mobile (secondary — list/view/download, reusing the pattern feature 014 established for
invoice PDF download). No caregiver-tablet impact — caregivers have no billing/fiscal
interaction, same as 014.

### User Impact

This enables a director to generate every enrolled child's legally required annual fiscal
attestation in one bulk action at year-end, resulting in parents receiving the document they need
to claim childcare costs on their Belgian tax return without the director preparing each one by
hand.

### UX Requirements

**Persona**: Director (desktop web, per `platform-rules.md`'s Director Web section) generating
and correcting attestations for a whole tax year. Parent (mobile, per `platform-rules.md`'s
Parent Mobile App section) finding and downloading their own child's attestation, following the
same warm, low-friction pattern already established for invoice downloads.

**Platform**: Director web (primary). Parent mobile (secondary, read-only). No caregiver-tablet
surface.

**User job (director)**: "At year-end, generate every eligible child's fiscal attestation in one
action, see which ones are done, and fix an individual one if a parent reports an error — without
manually calculating anyone's paid amount."

**User job (parent)**: "Find and download my child's tax certificate for the year, the same way I
already find my invoices."

**Success criteria**:

- A director can bulk-generate attestations for every eligible child in a chosen tax year from a
  single action, with per-child status visible afterward.
- A director can regenerate one child's attestation without re-running the whole batch.
- A parent can locate and download their child's attestation in the parent app within 3 taps or
  fewer, mirroring the existing invoice-download flow.
- No attestation, at any point, contains a parent's or child's national registry number (NRN).

**Main flow (director)**: Director opens the Fiscal Attestations screen (its own top-level
director-web nav entry, matching the flat sidebar structure `invoices` already uses — not nested
under a broader "Billing" grouping, since none exists) → selects a tax year → triggers bulk
generation → sees a per-child list with generation status (generated / no paid invoices this year
/ failed) → can download or regenerate any individual row.

**Main flow (parent)**: Parent opens the existing Invoices area (or an adjacent "Documents"/
"Tax certificates" entry — exact placement decided in plan against the existing parent-mobile
invoices screen) → selects their child's attestation for a tax year → downloads the PDF, reusing
the established `expo-file-system`/`expo-sharing` download pattern.

**Loading/empty/error states**: Director — empty state before any generation for a chosen year
("No attestations generated yet for {year}"); per-child "no paid invoices this year" state (not
an error — some enrolled children simply have nothing to attest); bulk-generation in-progress
state (a longer-running action, not a blocking spinner — mirrors the batch nature of 014's
`GenerateInvoicesCommand`). Parent — empty state if no attestation exists yet for a given year
("Not available yet — check back after your KDV generates this year's tax certificates").

**Accessibility**: Standard director-web table/list and parent-mobile screen accessibility
already established by 014 — no new accessibility surface beyond existing invoice-area patterns.

**Offline behavior**: Director web has no offline mode (unchanged). Parent mobile: downloading an
attestation requires connectivity, same as invoice PDF download — the existing offline banner
pattern applies if attempted while offline.

### Technical Requirements

**API impact**: New endpoints — bulk-generate attestations for a tax year, list attestations
(director, per year), regenerate one attestation, and a parent-facing list/download mirroring
`GetParentInvoicesQuery`/invoice-PDF-download's existing shape.

**Data-model impact**: A new tenant-scoped `FiscalAttestation`-shaped entity (one row per child +
tax year + location, since `Location.Erkenningsnummer`/`Location.Address` are per-location and
must appear correctly on the PDF — see Edge Cases) holding up to four rate periods (period start/
end, day count, amount, optional daily rate), a total amount, and a reference to the generated
PDF.

**Security considerations**: The NRN/SSIN MUST NEVER be persisted anywhere in this system — no
column, log, or intermediate structure captures it; the PDF carries a blank field the parent
fills in themselves. PDF access follows the same tenant-scoped, signed-URL-or-equivalent
authorization pattern already established for GCS-backed documents in this codebase (health
attachments, feature 013c) — see Assumptions for why this differs from invoice PDFs' on-demand,
unstored pattern.

**Performance considerations**: Bulk generation runs across potentially hundreds of children at
once — must not block a single HTTP request indefinitely; follows this codebase's existing
per-item-loop batch pattern (`GenerateInvoicesCommand`, 014) with per-child failure isolation so
one child's generation failure doesn't abort the whole batch.

**Testing requirements**: Happy-path bulk generation; mid-year daily-rate change producing a
correct multi-period breakdown; child departed mid-year (attestation covers only enrolled
months); child transferred between locations mid-year (separate attestations per location);
regeneration/correction overwriting a prior attestation; a child with zero paid invoices for the
year is excluded from generation, not given a zero-amount attestation; an assertion proving NRN
is never persisted; tenant isolation on attestation access; a notification is sent to every
linked contact on generation and again on regeneration (FR-016), using dedicated copy distinct
from 014/014a's invoice/reminder/receipt notifications.

## Clarifications

No `[NEEDS CLARIFICATION]` markers were needed in the initial draft — the BACKLOG.md prompt
block, its verified Belcotax legal note, and the existing 014/014a invoicing precedent supplied
reasonable defaults for almost every decision (see Assumptions). One genuine ambiguity remained
material enough to resolve explicitly before planning, since it changes FR scope (whether a
notification integration is required):

### Session 2026-07-16

- Q: Does generating or regenerating a fiscal attestation notify the child's linked contacts
  (parents), or is it purely pull-based (a parent finds it themselves)? → A: Notify, using the
  same in-app `Notification` + best-effort push pattern already established by
  `InvoiceNotificationService` (014) and extended for reminders/receipts (014a) — an attestation
  is exactly the kind of "a new document is ready for you" event that pattern exists for, and
  building a silent, pull-only exception here would be inconsistent with every other document
  this platform hands to a parent. Recorded as FR-016 below.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director bulk-generates attestations for a tax year (Priority: P1)

At year-end, a director needs every enrolled (and previously enrolled) child's fiscal attestation
ready for parents, without calculating each one by hand.

**Why this priority**: This is the entire reason the feature exists — without bulk generation,
directors are back to manual per-child calculation, which is exactly what this feature replaces.

**Independent Test**: Can be fully tested by seeding several children with `Paid` invoices across
a tax year (including one with a mid-year daily-rate change), triggering bulk generation for that
year, and confirming a correct attestation PDF is produced per eligible child with an accurate
period breakdown and total — independent of regeneration or the parent-facing download.

**Acceptance Scenarios**:

1. **Given** several children with `Paid` invoices in a chosen tax year, **When** the director
   triggers bulk generation, **Then** the system produces one attestation per eligible child
   (and per location, if a child had invoices at more than one location that year), each showing
   the correct total amount paid.
2. **Given** a child whose daily rate changed mid-year (a new `Contract`), **When** their
   attestation is generated, **Then** it shows two periods with the corresponding start/end
   dates, day counts, and daily rates, rather than one blended period.
3. **Given** a child with no `Paid` invoices in the chosen tax year, **When** bulk generation
   runs, **Then** no attestation is created for that child, and the director-facing status
   reflects "no paid invoices this year" rather than an error.
4. **Given** a child who left the KDV mid-year, **When** their attestation is generated,
   **Then** it covers only the months they were enrolled and paid for, matching their actual
   paid invoices.
5. **Given** a newly generated attestation, **When** generation completes, **Then** every
   contact linked to the child receives a notification that it's ready.

---

### User Story 2 - Parent downloads their child's attestation (Priority: P1)

A parent needs their child's tax certificate to file their Belgian tax return and claim
childcare costs.

**Why this priority**: The generated document has no value until parents can actually retrieve
it — this is the second half of the feature's core value, alongside generation itself.

**Independent Test**: Can be fully tested by generating an attestation for a child (pre-seeded),
then opening the parent app as that child's linked contact and confirming the attestation can be
found and downloaded — independent of how generation happened.

**Acceptance Scenarios**:

1. **Given** a generated attestation for their child, **When** a parent opens the relevant area
   of the parent app, **Then** they can view and download the PDF.
2. **Given** no attestation has been generated yet for a given tax year, **When** a parent looks
   for one, **Then** they see a clear "not available yet" state, not an error or a blank screen.
3. **Given** a downloaded attestation, **When** the parent opens it, **Then** it contains no NRN
   field pre-filled — only a blank field for them to complete themselves.

---

### User Story 3 - Director corrects a single attestation (Priority: P2)

A parent reports that the amount on their attestation looks wrong (e.g., they already declared a
different figure, or a late payment correction happened after generation). The director fixes it
without re-running the whole year's batch.

**Why this priority**: Necessary for a legally-filed document to stay accurate, but the platform
is still functional without it at initial release of this feature — bulk generation (P1) and
parent download (P1) deliver the core value; this is a correction path on top.

**Independent Test**: Can be fully tested by generating an attestation, changing the underlying
paid-invoice data (e.g., marking an additional invoice paid), regenerating that one child's
attestation, and confirming the new PDF reflects the updated amount while the rest of the year's
batch is untouched.

**Acceptance Scenarios**:

1. **Given** an existing attestation for a child, **When** the director regenerates it, **Then**
   the system re-aggregates that child's current paid-invoice data and replaces the existing PDF
   with an updated one — no duplicate attestation is created for the same child/tax year/
   location.
2. **Given** a director regenerates one child's attestation, **When** they check the rest of the
   batch, **Then** every other child's attestation for that tax year is unaffected.
3. **Given** an attestation the parent has already downloaded, **When** the director regenerates
   it, **Then** the parent sees the corrected version on their next visit — not a cached, stale
   one.

---

### Edge Cases

- What happens when a child's daily rate changed more than 3 times within one tax year (more
  than 4 distinct periods)? The system consolidates the oldest overflow periods into the earliest
  retained period (summing their days and amounts, leaving the daily rate blank for that merged
  period since it no longer reflects a single rate) so the total remains accurate even though the
  per-period breakdown is coarser — an intentionally rare case given real-world contract-change
  frequency.
- What happens when a child was enrolled at more than one location within the same tax year
  (a transfer)? A separate attestation is generated per location, each showing only that
  location's periods, name, address, KBO, and erkenningsnummer — required because
  `Location.Erkenningsnummer` is per-location, and the official attest must state the correct
  license number for the site the care was actually provided at.
- What happens when a child has `Paid` invoices for the year but their linked contact(s) changed
  since (e.g., a different parent is now primary)? The attestation shows the currently linked
  primary contact's name at generation/regeneration time — not a historical snapshot of who was
  primary during the invoiced months, consistent with how 014's invoice PDFs already resolve the
  "addressed to" contact.
- What happens if bulk generation is triggered again for a tax year that already has
  attestations? Existing attestations are left as-is (not silently overwritten by a bulk re-run)
  — only children without an existing attestation for that year are newly generated; correcting
  an existing one requires the explicit per-child regenerate action (User Story 3), so a
  director's deliberate correction is never accidentally undone by a routine re-run.
- What happens when a single child's generation fails during a bulk run (e.g., a rendering
  error)? The failure is isolated to that child — the rest of the batch completes, and the
  failed child's status is shown as "failed," retryable individually.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Directors MUST be able to bulk-generate fiscal attestations for a chosen tax year,
  covering every child with at least one `Paid` invoice in that year.
- **FR-002**: The system MUST compute each attestation's amount exclusively from the child's
  `Paid` invoices for the chosen tax year — never from the gross KDV rate, draft/sent/unpaid
  invoices, or any other source.
- **FR-003**: A child with no `Paid` invoices in the chosen tax year MUST NOT receive a
  generated attestation; the director-facing status MUST distinguish this case from a failure.
- **FR-004**: Each attestation MUST break the paid amount into up to four contiguous periods,
  each with a start date, end date, a billable-day count (present days plus unjustified-absent
  days — the same day count each period's underlying paid invoices were themselves billed on,
  not calendar days or attendance-only days), amount paid, and (where a single daily rate
  applies) the daily rate — supporting a child whose daily rate changed mid-year via a new
  contract. If more than four distinct periods would result, the system MUST consolidate the
  oldest overflow periods into the earliest retained one, per the Edge Cases section.
- **FR-005**: If a child had `Paid` invoices at more than one location within the same tax year,
  the system MUST generate a separate attestation per location, each reflecting only that
  location's periods, name, address, KBO/ondernemingsnummer, and erkenningsnummer.
- **FR-006**: Each attestation PDF MUST include: the KDV's name, address, KBO/ondernemingsnummer,
  and erkenningsnummer (if set on the location); the parent contact's first and last name; the
  child's first name, last name, and date of birth; the tax year; a blank field for the parent to
  fill in their own NRN; the per-period breakdown; the total amount paid across all periods;
  certification type code 1; the official Opgroeien declaration wording; and a signature line for
  the KDV responsible.
- **FR-007**: The system MUST NEVER store a parent's or child's national registry number (NRN /
  SSIN) anywhere in the database, in any log, or in any intermediate data structure — the PDF's
  NRN field is always blank, for the parent to complete themselves.
- **FR-008**: Directors MUST be able to regenerate a single child's (and, per FR-005, single
  location's) attestation independently of the full batch, re-aggregating that child's current
  paid-invoice data and replacing the previously generated PDF — without creating a duplicate
  attestation for the same child/tax year/location, and without affecting any other child's
  already-generated attestation.
- **FR-009**: Re-running bulk generation for a tax year that already has some attestations MUST
  only generate attestations for children that don't yet have one for that year — it MUST NOT
  silently overwrite an existing attestation (only the explicit per-child regenerate action,
  FR-008, does that).
- **FR-010**: If generation fails for an individual child during a bulk run, the failure MUST be
  isolated — the rest of the batch MUST complete, and the failed child's status MUST be visible
  and individually retryable.
- **FR-011**: Parents MUST be able to view and download their own child's generated attestation
  from the parent app, restricted to attestations for children they are a linked contact of.
- **FR-012**: Directors MUST be able to see, per tax year, the generation status of every
  eligible child's attestation (generated / no paid invoices this year / failed).
- **FR-013**: All user-facing strings introduced by this feature MUST use i18n keys with NL/FR/EN
  translations, matching every prior feature's convention.
- **FR-014**: The data model MUST retain enough structure per period (start/end date, day count,
  amount, optional daily rate) to support future Belcotax Fiche 281.86 electronic submission
  (feature 019 item 5) without requiring a schema migration when that feature is built.
- **FR-015**: This feature MUST NOT collect, store, or require the parent's NRN/SSIN at any
  point — that remains explicitly out of scope until a future electronic-submission feature
  needs it.
- **FR-016**: When an attestation is generated or regenerated, the system MUST notify every
  contact linked to the child, using the same in-app `Notification` + best-effort push pattern
  established by `InvoiceNotificationService` (014) and its reminder/receipt extensions (014a),
  with dedicated copy distinct from invoice/reminder/receipt notifications.

### Key Entities

- **Fiscal Attestation**: Represents one child's annual fiscal certificate for a given tax year
  and location — up to four rate periods (each with start/end date, day count, amount, optional
  daily rate), a total amount paid, a reference to the generated PDF, and generation timestamps.
  One per (child, tax year, location); regenerating replaces the existing one rather than
  creating a new record (FR-008/FR-009).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can generate attestations for every eligible child in a chosen tax year
  in a single bulk action, with per-child status visible immediately after.
- **SC-002**: 100% of attestations for a child whose daily rate changed mid-year show an accurate
  multi-period breakdown matching their actual paid invoices.
- **SC-003**: A director can correct a single child's attestation without re-running or disturbing
  the rest of that tax year's batch.
- **SC-004**: A parent can locate and download their child's attestation in the parent app within
  3 taps or fewer, mirroring the ease of the existing invoice-download flow.
- **SC-005**: Zero attestations, in any state, ever contain a pre-filled NRN/SSIN value.
- **SC-006**: Zero attestations are generated for a child with no paid invoices in the chosen tax
  year.

## Assumptions

- **Attestations are stored, not rendered on-demand** — unlike 014's invoice PDFs and 014a's
  betalingsbewijs (both explicitly rendered fresh from live state and never persisted, per their
  research/spec notes). This is a deliberate departure, not an oversight: BACKLOG.md's own
  015 prompt block specifies a `pdf_gcs_path` field, and a fiscal document a parent files with
  the tax authority benefits from being a stable, retrievable snapshot of what was declared —
  matching this codebase's established GCS-signed-URL pattern (`Gcs<Subject>Storage`, used for
  health attachments/profile photos/group-activity photos) rather than 014's on-demand pattern.
- Regenerating an attestation overwrites the existing PDF/period data in place rather than
  keeping multiple historical versions — consistent with 014's own regenerate-in-place precedent
  (before `Paid`) and with the BACKLOG prompt's framing ("Director can regenerate and re-send").
- "Tax year" means the Belgian calendar tax year (January–December), matching how Belgian
  childcare fiscal attestations are filed.
- The exact official Opgroeien/FOD Financiën declaration wording (FR-006) is sourced from the
  referenced official documentation during implementation — its precise legal text is a content
  detail, not a scope decision, and doesn't block this spec.
- Belcotax-on-web electronic submission remains entirely out of scope for this feature (manual
  director entry satisfies the legal deadline for MVP, per the verified 2026-07 legal note in
  BACKLOG.md) — feature 019 item 5 owns the automated filing path.
- No multi-child/sibling bundling applies — an attestation is inherently one child's individual
  document, unrelated to 014's family-bundling invoicing option (which affects invoice grouping,
  not attestation content).
- Since a child can only be linked to one primary contact at a time (per `ChildContact`), and
  Belgian tax attestations name the person who actually paid, the attestation's "parent"
  identification uses the currently linked primary contact — matching 014's own invoice-PDF
  precedent (see Edge Cases) rather than reconstructing historical primary-contact assignment.
