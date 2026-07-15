# Feature Specification: Invoicing

**Feature Branch**: `014-invoicing`

**Created**: 2026-07-15

**Status**: Draft

**Input**: User description: "Build monthly invoice generation and payment tracking for private
KDVs. For each child with an active contract, compute billable days for the month (present days
+ unjustified absences − closure days, at the contract's daily rate) and generate an invoice with
a Belgian OGM structured payment reference and a QuestPDF PDF. Directors generate, send, and
mark invoices paid; parents view and download their invoices. Phase 1 = private KDVs only, no
IKT subsidy lines."

## Product Context

### Feature Type

Mixed (Data-model change + API-backend capability + User-facing UI on director web and parent
mobile).

### Primary Consumer

Director (generates monthly invoices, reviews/edits drafts, sends them, records payment).
Parent (views and downloads their own children's invoices — no billing action of their own).

### Workflow Boundary

**Billing & Payments** (`workflows.md` / `Workflows/billing.md` — this is the first feature in
that workflow; the detail file was created as part of this spec, per `workflows.md`'s
governance rules).

Actors: Director (generates, edits, sends, marks-paid, regenerates). Parent (views, downloads).
System (computes billable days from attendance/absence/closure records, generates the OGM
reference, renders the PDF, sends the existing email/push notification on send).

Actions: Director bulk-generates a month's invoices for a location → reviews/edits each draft
(optional extra charges) → sends (individually or in bulk) → parent is notified and can view/
download → director records payment when the bank transfer clears → an unpaid invoice past its
due date reads as overdue.

Data Flow: Attendance records (009) + day-reservation absence records (013a) + published closure
days (011) + the child's active contract (007, for `daily_rate_cents` and contracted weekdays) →
billable-day computation → `Invoice` row (draft) → director edit/send → PDF render (QuestPDF) +
OGM reference generation → parent-visible invoice + notification.

Outputs: A per-child, per-month, per-location invoice with a correct billable-day breakdown, a
unique OGM structured reference, and a downloadable PDF. A parent-visible invoice list with
payment status in plain language.

Cross-Platform Impact: Director web (generation, review, send, payment recording) and parent
mobile (view, download). No caregiver-tablet impact — caregivers have no billing interaction
(`Workflows/billing.md`).

### User Impact

This enables a director to generate accurate, correctly-computed monthly invoices with a valid
Belgian payment reference in a few clicks instead of manual spreadsheet work, and enables a
parent to find out what they owe and pay it correctly, without either side chasing typos in a
hand-written amount or payment reference.

### UX Requirements

**Persona**: Director (desktop web, per `platform-rules.md`'s Director Web section) for
generation/review/payment-tracking. Parent (mobile, per `platform-rules.md`'s Parent Mobile App
section) for viewing/downloading — warm, reassuring, no raw status strings or database language.

**Platform**: Web (director) and parent-mobile (parent). No caregiver-tablet surface.

**User job (director)**: "Generate this month's invoices for my location, correct in one pass,
with the right payment reference on each, without hand-computing a single day count."

**User job (parent)**: "See what I owe for my child's care this month, and find the reference I
need to pay it by bank transfer, without digging through paperwork."

**Success criteria**:

- A director can generate a full location's invoices for a month in a single bulk action, with
  billable days computed automatically from existing attendance/absence/closure data — no
  manual day-counting.
- Every generated invoice has a unique, checksum-valid OGM reference and a downloadable PDF
  containing every field a parent needs to pay correctly by bank transfer.
- A parent can find and download any invoice for their child within two taps/clicks of opening
  the app.
- A location that has never generated an invoice sees zero change to any other feature's
  behavior (attendance, contracts, closures) — invoicing only reads existing data, it never
  writes to it.

**Main flow (director)**: Director opens the Invoices section for a location and month →
clicks "Generate invoices" → the system computes and creates one draft invoice per child with
a contract active that month → director reviews the list, opens any invoice to check the
computed breakdown and optionally add an extra charge → sends one or all draft invoices →
watches invoices move from `sent` to `paid` as payments come in, marking each manually.

**Main flow (parent)**: Parent opens the Invoices section in the parent app → sees a list of
their children's invoices, most recent first, with plain-language status ("Awaiting payment",
"Paid", "Overdue") → opens one → downloads the PDF to see the full breakdown and the payment
reference.

**Loading/empty/error states**: An empty invoice list (no invoices generated yet for a location/
month, or a parent with none yet) shows a short explanatory sentence, not a blank table/screen.
A failed PDF download shows a clear retry affordance, matching every other download flow in this
codebase.

**Accessibility**: The director-web invoice table and generation/send/mark-paid actions follow
the same keyboard-operable, focus-visible patterns as every other director-web control in this
codebase (e.g. 013f's `ReservationSettingsForm`, 013a's day-reservations queue). No new
accessibility pattern is introduced.

**Offline behavior**: Parent-mobile has no persistent offline store for invoices in this phase
(matches 013e/013j's Menu tab precedent) — a failed fetch shows the load-failed state, not stale
cached data.

### Technical Requirements

**API impact**: New endpoints for invoice generation (bulk, per location/month), listing/
filtering (director and parent views), invoice detail, PDF download, send, mark-paid, and
regenerate. All director-facing endpoints reuse the existing `DirectorOnly` policy; parent
endpoints reuse the existing parent-contact-resolution authorization, scoped to only that
parent's own children's invoices.

**Data-model impact**: New `Invoice` entity (tenant schema) — `ChildId`, `ContractId`,
`LocationId`, `PeriodMonth` (first-of-month date), `Status`, `SubtotalCents`, `TotalCents`,
`LineItems` (JSONB), `OgmReference`, `SentAt`, `PaidAt`, `DueDate`, `CreatedAt`, `UpdatedAt`. No
changes to `Contract`, `Attendance`, `DayReservation`, or `ClosureDay` — invoicing only reads
them.

**Security considerations**: No new authorization boundary beyond the existing `DirectorOnly`/
parent-contact-resolution model, both already tenant-scoped. A parent must never be able to
read another family's invoice, or a `draft` invoice belonging to their own child (drafts are
director-only until sent).

**Performance considerations**: Bulk generation for a location/month is bounded by that
location's active-contract count (a few dozen to a few hundred children) — not a hot path, runs
on an explicit director action, not a background schedule.

**Testing requirements**: Backend integration tests (real PostgreSQL, constitution Principle V)
for: the billable-day computation (present/unjustified-absent/closure precedence, mid-month
contract start/end, zero-total months), the OGM modulo-97 checksum algorithm, one-invoice-per-
child-per-month-per-location uniqueness, split-location contracts producing two invoices,
draft-vs-sent parent visibility, regenerate-replaces-PDF-and-line-items behavior, and paid-
invoice immutability. Director-web component tests for the invoice list/detail/generation flow.
Parent-mobile component tests for the invoice list/detail/download flow.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Director generates a month's invoices for a location (Priority: P1)

A director wants to bill every family at their location for July. They open the Invoices
section, select the location and month, and generate invoices in one action — one per child
with a contract active that month, each with billable days computed automatically.

**Why this priority**: Without generation, nothing else in this feature has anything to act on
— it's the foundation every other story builds on.

**Independent Test**: With a location holding several children with a mix of full attendance,
justified absences, unjustified absences, and closure days that month, generate invoices and
confirm each child's computed line items match the billable-day rule exactly.

**Acceptance Scenarios**:

1. **Given** a location with three children holding active contracts for the selected month,
   **When** the director generates invoices, **Then** exactly three draft invoices are created,
   each showing the correct present/unjustified-absent/closure-excluded day counts and a total
   computed at the contract's `daily_rate_cents`.
2. **Given** a child's contract started mid-month, **When** invoices are generated, **Then**
   that child's invoice only counts days from the contract's start date through month end.
3. **Given** a child's contract ended mid-month, **When** invoices are generated, **Then** that
   child's invoice only counts days from month start through the contract's end date.
4. **Given** a child holds active contracts at two different locations, **When** invoices are
   generated for each location separately, **Then** two independent invoices are created — one
   per location — each reflecting only that location's attendance/closure data.
5. **Given** an invoice was already generated for a child/contract/location/month, **When** the
   director generates invoices for that location/month again, **Then** the existing invoice is
   not duplicated (re-generation of an already-generated, not-yet-sent invoice recomputes it in
   place; see User Story 4 for the sent/paid regeneration rules).

---

### User Story 2 - Director reviews, sends, and tracks payment (Priority: P1)

A director reviews a generated draft invoice, adds a one-off extra charge, sends it to the
parent, and later marks it paid once the bank transfer arrives.

**Why this priority**: Generation alone delivers no value until a parent can actually see and
pay an invoice, and the director can track whether they did.

**Independent Test**: Open a draft invoice, add an extra charge, send it, confirm it becomes
parent-visible with the extra charge included in the total, then mark it paid and confirm the
status and paid date persist.

**Acceptance Scenarios**:

1. **Given** a draft invoice, **When** the director adds an extra charge line item and sends
   it, **Then** the invoice's total includes the extra charge, its status becomes `sent`, and
   it becomes visible to the parent.
2. **Given** a draft invoice, **When** the director sends it without any edits, **Then** its
   computed line items and total are unchanged and it becomes parent-visible.
3. **Given** several draft invoices for a location/month, **When** the director sends them as a
   batch, **Then** every draft invoice in that batch transitions to `sent` and each parent is
   notified.
4. **Given** a `sent` invoice past its due date with no payment recorded, **When** the director
   views the invoice list, **Then** it displays as overdue.
5. **Given** a `sent` (or overdue) invoice, **When** the director records payment with a date,
   **Then** its status becomes `paid`, the payment date is stored, and it no longer displays as
   overdue.

---

### User Story 3 - Parent views and downloads their invoices (Priority: P1)

A parent opens the app to check what they owe this month and find the payment reference for
their bank transfer.

**Why this priority**: This is the actual point of sending an invoice — without a parent-facing
view, generation and sending deliver no value to the family.

**Independent Test**: With a `sent` invoice for a parent's child, confirm it appears in the
parent's invoice list with the correct amount and status, and that the downloaded PDF contains
the OGM reference and every other required field.

**Acceptance Scenarios**:

1. **Given** a parent has one child with a `sent` invoice, **When** they open the Invoices
   section, **Then** they see it listed with the correct amount and a plain-language status.
2. **Given** a parent has two children each with invoices at the same or different locations,
   **When** they open the Invoices section, **Then** they see every invoice for every child,
   clearly attributed to the right child.
3. **Given** a child's invoice is still `draft`, **When** the parent opens the Invoices section,
   **Then** that invoice does not appear at all.
4. **Given** a `sent` invoice, **When** the parent downloads its PDF, **Then** the PDF contains
   the KDV's name/address/KBO/erkenningsnummer (if set), the parent and child's name, the
   billing period, the line-item breakdown, the total due, the due date, the OGM reference, and
   the KDV's bank account number.

---

### User Story 4 - Director regenerates an invoice after correcting attendance (Priority: P2)

A director realizes an attendance record for a child was wrong, corrects it, and needs the
already-generated invoice to reflect the correction.

**Why this priority**: Attendance corrections are already a normal part of this codebase (009),
and invoicing must not become a source of stale, incorrect financial records whenever one
happens — but this is a correction path, not the primary generate/send/pay flow, so it ranks
below the P1 stories.

**Independent Test**: Correct an attendance record underlying a `sent` invoice, regenerate it,
and confirm the line items, total, and PDF reflect the correction while the OGM reference stays
the same (it identifies this invoice, not this specific PDF revision).

**Acceptance Scenarios**:

1. **Given** a `draft` invoice, **When** the underlying attendance data changes and the director
   regenerates it, **Then** the line items and total are recomputed with no parent notification
   (it was never sent).
2. **Given** a `sent` invoice, **When** the director regenerates it, **Then** the line items,
   total, and PDF are recomputed and replaced, the OGM reference is unchanged, and the parent is
   notified again.
3. **Given** a `paid` invoice, **When** the director attempts to regenerate it, **Then** the
   system rejects the attempt — a paid invoice is immutable.

---

### Edge Cases

- A child has zero present days and zero unjustified absences for the month (every day was
  either a justified absence or a closure day): the invoice is still generated, with a total of
  0, for the audit trail (Assumptions).
- A child's contract exists for the month but was never active (e.g. cancelled before its start
  date): no invoice is generated for that contract.
- A location has published closure days that overlap days the child wasn't even contracted for
  (e.g. before their contract start): those dates simply don't enter the computation either way.
- Two attempts to generate invoices for the same location/month run back-to-back (e.g. a
  director double-clicks "Generate"): no duplicate invoices are created per (child, contract,
  location, month).
- A director tries to send an invoice that's already `sent` or `paid`: rejected — sending only
  applies to `draft` invoices.
- A director tries to mark a `draft` invoice paid without sending it first: rejected — payment
  can only be recorded on a `sent` or overdue invoice.
- The OGM checksum computation must be deterministic and produce a different reference for every
  invoice (collision would break bank payment matching).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST let a director bulk-generate draft invoices for every child
  holding a contract active at any point during a selected location and month.
- **FR-002**: For each generated invoice, the system MUST compute billable days as: present days
  plus unjustified-absence days, minus any day that is a published closure day for that
  location, restricted to the portion of the month the contract was actually active (mid-month
  start/end honored) — billed at the contract's `daily_rate_cents`. Justified absences (per
  013a's day-reservation records) are never billed.
- **FR-003**: The system MUST NOT create more than one invoice per (child, contract, location,
  month) — regenerating an already-generated `draft` invoice updates it in place rather than
  creating a duplicate.
- **FR-004**: The system MUST generate a unique, checksum-valid Belgian OGM structured payment
  reference (`+++XXX/XXXX/XXXXX+++` format, 12 digits, modulo-97 checksum) for every invoice at
  creation time, and MUST NOT reuse a reference across invoices.
- **FR-005**: The system MUST render an invoice PDF (QuestPDF) containing: the KDV's name,
  address, KBO/ondernemingsnummer (if set on the location/organisation), erkenningsnummer (if
  set), the parent's name, the child's name, the billing period (month + year), the line-item
  breakdown, the total due, the due date, the OGM reference (visually prominent), and the KDV's
  bank account number.
- **FR-006**: The system MUST let a director add manual extra-charge line items (a label and an
  amount in cents) to a `draft` invoice before sending; extra charges MUST be included in the
  invoice's total.
- **FR-007**: The system MUST let a director send one or more `draft` invoices, which MUST: set
  status to `sent`, make the invoice visible to the parent, generate/store its PDF, and notify
  the parent via the existing notification channel(s).
- **FR-008**: The system MUST NOT make a `draft` invoice visible to any parent — only `sent`,
  `paid`, or overdue invoices are parent-visible.
- **FR-009**: The system MUST let a director manually record payment (with a payment date) on a
  `sent` (including overdue) invoice, setting its status to `paid`.
- **FR-010**: The system MUST treat a `sent` invoice whose due date has passed with no recorded
  payment as overdue for display/filtering purposes, without requiring a separate stored status
  transition or any new background-job infrastructure.
- **FR-011**: The system MUST let a director regenerate a `draft` or `sent` invoice's line items
  and PDF after underlying attendance/absence data changes; regenerating a `sent` invoice MUST
  re-notify the parent. The OGM reference MUST NOT change on regeneration.
- **FR-012**: The system MUST reject any attempt to regenerate, edit, or otherwise alter a
  `paid` invoice.
- **FR-013**: The system MUST reject sending an invoice that is not currently `draft`, and MUST
  reject marking an invoice paid unless it is currently `sent` (or overdue).
- **FR-014**: A child holding active contracts at more than one location MUST receive one
  independent invoice per location for the same month — never a single combined invoice.
- **FR-015**: The system MUST let a director view a filterable/sortable list of invoices for a
  location (status, month) and a parent view a list of all invoices across their own children
  only.
- **FR-016**: The system MUST let a director and an authorized parent download an invoice's PDF.
- **FR-017**: All money values MUST be stored and computed as integer cents; the system MUST
  NEVER use a floating-point type for any monetary field or computation.
- **FR-018**: All new director-facing and parent-facing strings introduced by this feature MUST
  be provided through the existing i18n systems (NL/FR/EN on web, NL/FR/EN on parent-mobile).
- **FR-019**: The `Invoice` line-items schema MUST include duration-categorized present-day
  counts (`days_min5u`, `days_min11u`) for future Belcotax/Opgroeien reporting compatibility,
  even though nothing consumes them yet.
- **FR-020**: The `Invoice` data model MUST accommodate future IKT subsidy fields as nullable —
  Phase 1 MUST NOT compute or display any subsidy-adjusted amount.

### Key Entities

- **`Invoice`** (new, tenant schema): one per (child, contract, location, month). Fields:
  `ChildId`, `ContractId`, `LocationId`, `PeriodMonth`, `Status` (`draft`/`sent`/`paid`, with
  `overdue` a computed view over `sent`), `SubtotalCents`, `TotalCents`, `LineItems` (JSONB:
  present days, unjustified-absent days, daily rate, excluded closure-day count,
  duration-categorized counts, extra charges array), `OgmReference`, `SentAt`, `PaidAt`,
  `DueDate`, `CreatedAt`, `UpdatedAt`.
- **`Contract`** (existing, 007, unchanged): source of `daily_rate_cents` and the contracted
  date range/weekdays an invoice's billable-day computation reads.
- **`Attendance`**/**`DayReservation`**/**`ClosureDay`** (existing, 009/013a/011, unchanged):
  read-only inputs to the billable-day computation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A director can generate a full location's monthly invoices in a single action,
  with zero manual day-counting.
- **SC-002**: 100% of generated invoices have a checksum-valid, unique OGM reference and a
  billable-day total matching the present + unjustified-absent − closure-day rule exactly.
- **SC-003**: A parent can locate and download any of their children's sent invoices within two
  taps/clicks of opening the Invoices section.
- **SC-004**: A location that never generates an invoice shows zero behavioral change to any
  other existing feature (attendance, contracts, closures, day-reservations).
- **SC-005**: 100% of `paid` invoices remain byte-for-byte unchanged by any subsequent
  regenerate/edit attempt.

## Assumptions

- Invoice due date defaults to a fixed number of days after generation unless
  `/speckit-clarify` surfaces a stronger precedent for a per-location configurable setting (the
  013f `ReservationSettingsForm` pattern) — resolved during clarification, not guessed here.
- A zero-total invoice (no billable days that month) is still generated, for the audit trail, per
  the feature's own prompt language — not skipped.
- Invoice generation is a manual director-initiated action, not an automatic scheduled job — no
  background-job infrastructure exists in this codebase yet, and introducing one for a single
  monthly trigger would be disproportionate (constitution's Monolith-First Simplicity).
- "Overdue" is a computed view (`status = sent AND due_date < today`), not a separately stored
  status value — avoids a background process whose only job would be flipping one field.
- Sending an invoice reuses the existing parent notification channel(s) (email via the existing
  `IEmailSender`, in-app/push per 009's precedent) — no new communication mechanism is built for
  this feature.
- A `paid` invoice is immutable; correcting a paid invoice's underlying attendance data does not
  retroactively change that invoice — a credit-note/adjustment mechanism for this case is out of
  scope (a candidate future backlog item, not built here).
- Extra-charge line items are free-form (label + amount), added manually per invoice by the
  director before sending — there is no catalog/preset of standard extra charges in this phase.
- The existing `Location`/organisation profile fields (KBO/ondernemingsnummer,
  erkenningsnummer, bank account number) are assumed to already exist or be addable as simple
  nullable fields; if any is missing from the current data model, adding it is in scope for this
  feature (it's required PDF content per the feature's own prompt).
