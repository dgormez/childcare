# Billing & Payments Workflow

## Purpose

Manage the financial relationship between families and the center: computing what a family
owes for a month of care, producing an invoice they can act on, and tracking whether it's been
paid.

### Trigger

A calendar month ends (or is ending), and every child with an active contract needs an invoice
reflecting that month's attendance.

### Actors

- Director (generates invoices, reviews/edits a draft before sending, marks an invoice sent,
  records payment, regenerates an invoice after correcting an attendance record, generates a
  SEPA direct debit batch, marks a returned debit, revokes a SEPA mandate — feature 026)
- Parent (views and downloads their child's invoices — no billing action of their own; payment
  happens by bank transfer or, once a SEPA mandate is signed via feature 024, by direct debit
  collection the director initiates — feature 026)
- System (computes billable days from attendance/absence/closure records, generates the OGM
  structured payment reference, renders the PDF, generates and schema-validates a SEPA pain.008
  batch — feature 026)

### Flow — monthly invoicing (feature 014)

1. For a given location and month, the director generates invoices for every child with a
   contract active at any point that month. One invoice per (child, contract, location) —
   a child with contracts at two locations gets two invoices.
2. For each invoice, the system computes billable days from that child's attendance record
   (009), day-reservation absence records (013a), and the location's published closure days
   (011): present days and unjustified-absence days are billed at the contract's daily rate;
   justified absences and closure days are not. See `spec.md`'s billable-day rule for the exact
   precedence.
3. Each invoice starts as `draft` — visible to the director only, not yet visible to the
   parent. The director can review the computed line items and, before sending, add manual
   extra-charge line items (e.g. a registration fee) the automatic computation doesn't cover.
4. The director sends the invoice (individually or as a batch for the whole month/location).
   Sending: (a) transitions status to `sent`, (b) makes the invoice visible to the parent in
   the parent app, (c) generates and stores the PDF, and (d) notifies the parent (existing
   email/push channels, feature 003/009 precedent — no new notification mechanism).
5. The parent views their invoice list and downloads the PDF. The OGM structured reference on
   the PDF is what they use for the bank transfer — the app never collects or processes a
   payment itself (Phase 1; see feature 014's Out of scope).
6. The director records payment once the bank transfer clears — manually (marks `paid` with a
   payment date), or automatically by uploading their bank's CODA statement export (feature
   025): the system matches each statement transaction against an invoice's structured payment
   reference (or, failing that, a director-confirmable amount+IBAN suggestion) and marks the
   matching invoice paid the same way a manual mark-paid does. An invoice can never be marked
   paid twice — a transaction that would re-credit an already-paid invoice is instead flagged
   for the director's review, never silently reapplied. A payment less than an invoice's
   remaining total is recorded as a partial payment without closing the invoice, until later
   payments bring the total to zero.
7. An invoice past its due date with no payment recorded is treated as overdue for display and
   filtering purposes — this is a computed view of `status = sent AND due_date < today`, not a
   separate stored transition, since no background-job infrastructure exists in this codebase
   yet and a single derived status doesn't justify introducing one.
8. If the director corrects an attendance record after an invoice was generated, they can
   regenerate a `draft` or `sent` (not yet paid) invoice: the line items and PDF are
   recalculated and replace the old ones, and the parent is notified again if the invoice had
   already been sent. A `paid` invoice is immutable — regenerating it is not offered; a
   correction after payment would need a credit-note/adjustment mechanism, which is out of this
   feature's scope.

### Flow — SEPA direct debit batch collection (feature 026)

An alternative collection-initiation method to step 5's bank transfer, for families with a
signed SEPA mandate (feature 024). Plugs into the existing paid-tracking mechanism (step 6)
rather than replacing it.

1. For a location and month, the director reviews `sent` invoices split into eligible (the
   family's contract has a signed, non-revoked SEPA mandate) and excluded (no mandate, revoked
   mandate, or a non-positive amount) groups.
2. The director selects some or all eligible invoices, sets an execution date (at least one
   business day out), and generates a pain.008.001.02 XML batch. The system validates the file
   against the official schema before offering it for download; a failure blocks the download
   and changes nothing. On success, every included invoice transitions `sent → pending_debit`
   and the batch is recorded for later reference (location, execution date, who generated it,
   which invoices).
3. The director uploads the downloaded file to their bank's portal — outside this system.
4. When the bank's CODA statement later confirms the collection, step 6's existing CODA import
   (feature 025) matches and marks the invoice `paid` exactly as it would a regular transfer — a
   `pending_debit` invoice is just as "open" to that reconciliation as a `sent` one.
5. If a debit is instead returned by the bank (insufficient funds, closed account, disputed
   mandate), the director marks the invoice returned with a reason; it reverts to `sent`,
   re-entering normal overdue follow-up and eligible for a future batch. No auto-retry.
6. A parent's mandate can be revoked by the director on the family's request (e.g., a closed
   bank account) — this immediately excludes every current and future invoice for that contract
   from step 1's eligible group until a new mandate is signed via feature 024's existing
   invitation flow. There is no direct edit of a signed mandate's IBAN — only revoke-and-resign.

### Applications

Director Web:

- Generate a month's invoices for a location (bulk action).
- Review/edit a draft invoice (add extra charges) before sending.
- Send, regenerate, and mark-paid actions per invoice.
- Filterable/sortable invoice list (status, location, month) — high-density table, per
  `platform-rules.md`'s Director Web section.
- Download any invoice's PDF.
- Upload a CODA bank statement and review the resulting reconciliation: auto-matched
  transactions, director-confirmable suggested matches, and a manual-review queue for anything
  unrecognized, a duplicate payment, or a payment against an already-closed invoice (feature 025).
- Review batch-eligible invoices, generate and download a SEPA pain.008 batch, view batch
  history, mark a returned debit, and revoke a family's SEPA mandate (feature 026).

Parent Mobile:

- View the list of their children's invoices (sent/paid/overdue only — never a draft).
- Download an invoice's PDF.
- See payment status in plain language, not raw status strings.

Caregiver Tablet: no billing interaction — caregivers do not see or handle any financial data.

### Principles

- Money is always cents (integers), never floats — see `constitution.md`'s Technology Stack
  Constraints.
- A parent never sees a draft invoice — only an amount the center has actually committed to
  billing them.
- A paid invoice is a financial record and is never silently altered.
- No in-app payment collection in this phase — Belgian parents pay by bank transfer using the
  OGM reference; the app's job is to make that reference correct and easy to find, not to
  process the payment itself.
