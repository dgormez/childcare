# Feature Specification: Invoice Payments Plus

**Feature Branch**: `014a-invoice-payments-plus`

**Created**: 2026-07-16

**Status**: Draft

**Input**: User description: "Follow-up on the shipped 014-invoicing: online payment links,
automatic payment reminders and payment receipts. 014 itself is DONE — this feature only ADDS
endpoints/jobs/screens on top of the existing invoices table and lifecycle; do not change 014's
billable-day rules or PDF. PSP decision (product-owner, 2026-07-16): Mollie Connect for
Platforms, behind an `IPaymentProvider` abstraction. Automatic reminders for overdue invoices
(configurable per location). Automatic payment receipt (betalingsbewijs) on paid."

## Clarifications

### Session 2026-07-16

- Q: When a parent taps "Pay now" and a pending Mollie payment already exists for that invoice,
  does the system reuse it or create a new one? → A: Reuse the existing pending/open payment if
  one exists and hasn't expired, failed, or been cancelled; create a new one only when none
  exists or the prior one is no longer active. Keeps exactly one active `Payment` reference per
  invoice at a time, which keeps webhook idempotency (FR-009) simple.
- Q: Does the reminder job repeat indefinitely at the configured cadence until paid, or stop
  after a maximum count? → A: Caps at 3 automatic reminders per invoice; after the cap, no
  further automatic reminders are sent (the director can still follow up manually via 014's
  existing tools). An indefinitely-repeating reminder would read as a collections process,
  contradicting the "calm and trustworthy" product principle (`design-system.md`).
- Q: What are the default reminder delay/cadence values a director sees when first enabling
  reminders for a location? → A: 3 days after the due date for the first reminder, repeating
  every 7 days thereafter (subject to the 3-reminder cap above). Directors may adjust both
  values per FR-012; this is only the pre-filled starting point.
- Q: What must the betalingsbewijs (receipt) contain — a simple payment confirmation, or a
  legal/tax-purpose document? → A: A simple payment confirmation (KDV identity, child/parent
  name, invoice reference, amount paid, date paid) — not a tax document. Feature 015 (fiscal
  attestations, unstarted) remains the correct home for NRN/tax-specific legal formatting;
  conflating the two would duplicate and risk contradicting 015's not-yet-designed content
  requirements.
- Q: Does online payment need to support 014's "sibling family bundling" concept? → A: Not
  applicable — 014 shipped without any bundled-invoice concept (`Invoice` is one row per
  `ChildId`/`ContractId`/`LocationId`/`PeriodMonth`, ships no bundling column or entity); online
  payment is inherently per-invoice, same granularity as everything else in 014.

## Product Context

### Feature Type

Mixed (API-backend capability + User-facing UI on director web and parent mobile).

### Primary Consumer

Parent (pays via a hosted link, receives a receipt). Director (connects the organisation's
Mollie account, configures reminder cadence, sees payment status). System (webhook handling,
recurring reminder job, receipt generation).

### Workflow Boundary

**Billing & Payments** (`workflows.md` / `Workflows/billing.md`, established by feature 014).

Actors: Director (connects Mollie, configures reminders). Parent (opens payment link, pays,
receives receipt). System (webhook handler, reminder job, receipt generator).

Actions: Director connects the organisation's Mollie account via OAuth from director web →
system stores the connected sub-merchant account → parent opens an invoice, taps "Pay now" →
system creates a Mollie payment against the organisation's connected account and returns a
hosted payment URL → parent completes payment on Mollie's hosted page → Mollie calls a webhook
→ system resolves the tenant and invoice from the payment reference (never from a client-
supplied tenant claim), verifies the payment status with Mollie, and marks the invoice paid
(reusing 014's existing `Sent → Paid` transition) → system generates and sends a betalingsbewijs
to the invoice's linked contacts. Separately, a recurring system job scans `Sent` invoices past
their due date and sends a reminder per the location's configured delay/cadence, until the
invoice is paid.

Data Flow: `Invoice` (014, `Sent` status) + the organisation's connected Mollie account → payment
link creation → Mollie hosted checkout → webhook → tenant/invoice resolution → `Invoice.Status =
Paid` (existing 014 transition, existing `PaidAt`/audit trail) → betalingsbewijs generation →
parent notification. Independently: `Invoice` (Sent, overdue) + `Location` reminder settings →
reminder job → parent notification.

Outputs: A paid invoice (identical end-state to 014's existing manual mark-paid), a
betalingsbewijs PDF available to the parent, and reminder notifications for overdue invoices.

Cross-Platform Impact: Director web (Mollie connection settings, reminder-cadence settings,
payment-link status visible on the existing invoice view). Parent mobile (a "Pay now" action on
an invoice, a receipt available after payment). No caregiver-tablet impact — caregivers have no
billing interaction, same as 014.

### User Impact

This enables a parent to pay an invoice online in a couple of taps and receive an automatic
receipt, and enables a director to collect payments faster with fewer manual "did they pay yet"
follow-ups, without operating a second banking relationship — payments settle directly into the
organisation's own bank account via Mollie.

### UX Requirements

**Persona**: Parent (mobile, per `platform-rules.md`'s Parent Mobile App section) for paying and
receiving a receipt — warm, reassuring, no raw payment-processor language. Director (desktop
web, per `platform-rules.md`'s Director Web section) for connecting Mollie and configuring
reminders.

**Platform**: Parent-mobile app and director web. No caregiver-tablet surface.

**User job (parent)**: "Pay what I owe for my child's care without leaving the app or hunting
for a bank reference, and get proof once it's done."

**User job (director)**: "Let parents pay online without me having to chase bank transfers, and
have the system nudge them automatically if they forget."

**Success criteria**:

- A parent can go from an invoice in the app to a completed payment in three taps or fewer
  (invoice detail → "Pay now" → complete on the hosted payment page).
- A director can connect the organisation's Mollie account in a single guided OAuth flow, with
  no manual document upload inside this app.
- An overdue invoice generates at least one reminder without any director action, per that
  location's configured cadence.
- A parent who pays online receives a receipt they can view or download without asking the
  director for one.
- A location that never connects Mollie sees zero change to its existing invoice flow — bank
  transfer with OGM remains fully functional exactly as 014 shipped it.

**Main flow (director)**: Director opens the organisation Settings page → clicks "Connect
Mollie" → completes Mollie's own hosted OAuth onboarding → returns to the app showing a
connected status → optionally configures the reminder delay and cadence on the relevant
location's existing Invoicing settings.

**Main flow (parent)**: Parent opens an unpaid invoice → taps "Pay now" → is taken to Mollie's
hosted payment page → completes payment → returns to the app, which shows the invoice as paid
and offers the receipt.

**Loading/empty/error states**:

- Director: not-connected state (default, shows "Connect Mollie" call to action), connecting
  state (mid-OAuth-redirect), OAuth failure (clear retry affordance, no raw provider error
  text), connected state (shows the linked account, a disconnect action).
- Parent: payment-link generation failure (retry affordance — e.g. the organisation hasn't
  connected Mollie, or the PSP call failed), payment cancelled/abandoned on Mollie's page
  (invoice remains unpaid, "Pay now" still available), webhook-pending state (parent returns to
  the app before the webhook has landed — show a short "confirming your payment" state that
  resolves to paid once the webhook arrives, per FR-010), receipt not yet available (only exists
  once paid).

**Accessibility**: Standard parent-mobile and director-web accessibility already established by
014 — no new accessibility surface beyond the existing invoice screens' patterns.

**Offline behavior**: The payment link and Mollie's hosted page both require connectivity; the
existing offline banner pattern (per `design-system.md`) applies if the parent is offline when
attempting to pay — "Pay now" is disabled with the standard offline banner rather than allowed
to fail silently.

### Technical Requirements

**API impact**: New endpoints — create a payment link for an invoice, initiate/complete the
Mollie OAuth connection for an organisation, a public Mollie webhook endpoint (tenant-exempt,
resolves tenant from the payment reference), and director-web CRUD for per-location reminder
settings.

**Data-model impact**: A payment-provider connection record per organisation (Mollie account
reference, OAuth tokens, connected status) in the shared/public schema (a cross-tenant lookup is
required to resolve a webhook to a tenant without trusting the payload — see Key Constraints).
A payment attempt/record per invoice (provider payment ID, status, fee amount, tenant-scoped). A
reminder-settings record per location (delay, cadence, enabled flag) — same shape as 014's
`Location.InvoiceDueDays` precedent. The betalingsbewijs itself needs no separate stored
document — 014's invoice PDF is rendered on-demand from the invoice's current stored state,
never persisted to storage, and the same pattern applies here (an invoice's paid-state fields
don't change after `Paid`, so on-demand rendering is already idempotent/deterministic).

**Security considerations**: OAuth tokens for the connected Mollie account are secrets — stored
encrypted at rest, never logged, never returned to the client. The webhook endpoint is public
and must verify the payment with Mollie's API directly (not trust the webhook payload's claimed
status), and must resolve tenant/invoice only from data this system generated and controls (the
payment reference), never from any tenant/organisation identifier the caller supplies.

**Performance considerations**: Reminder job processes all overdue invoices across all tenants
on a recurring schedule (daily is the assumed default cadence granularity) — must complete
within a bounded window and not block interactive requests; the specific scheduling mechanism
(recurring job runner vs. externally triggered CLI command, matching this codebase's existing
CLI-command precedent) is a planning-phase decision. A failure processing one organisation's
invoices MUST NOT prevent the job from processing the remaining organisations — per-tenant
failures are isolated and logged, not fatal to the whole run.

**Testing requirements**: Webhook idempotency (duplicate delivery must not double-process a
payment), tenant-resolution correctness (a forged/malformed payload must not cross tenant
boundaries), the `IPaymentProvider` abstraction boundary (provider-specific types must not leak
past it), and the reminder job's cadence/delay logic.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Parent pays an invoice online (Priority: P1)

A parent has a `Sent` invoice they haven't paid by bank transfer yet. Instead of tracking down
their bank app and the OGM reference, they pay directly from the invoice screen.

**Why this priority**: This is the core value of the feature — faster payment collection is the
entire reason 014a exists. Without this, reminders and receipts have nothing to act on.

**Independent Test**: Can be fully tested by opening a `Sent` invoice for an organisation with a
connected Mollie account, tapping "Pay now", completing a test payment on Mollie's hosted page,
and confirming the invoice transitions to `Paid` and a receipt becomes available — independent
of reminders or the OAuth connection flow itself (which can be pre-seeded for this test).

**Acceptance Scenarios**:

1. **Given** a `Sent` invoice for an organisation with a connected Mollie account, **When** the
   parent taps "Pay now", **Then** they are taken to a Mollie-hosted payment page pre-filled
   with the correct invoice amount.
2. **Given** a completed payment on Mollie's hosted page, **When** Mollie calls the webhook,
   **Then** the invoice transitions from `Sent` to `Paid` exactly once, `PaidAt` is set, and the
   existing 014 audit trail records the transition.
3. **Given** an invoice for an organisation that has NOT connected Mollie, **When** the parent
   views the invoice, **Then** no "Pay now" action is shown and bank-transfer instructions
   (OGM reference) remain the only payment path, unchanged from 014.
4. **Given** a parent who abandons or cancels payment on Mollie's hosted page, **When** they
   return to the app, **Then** the invoice remains `Sent` (unpaid) and "Pay now" is still
   available to retry.

---

### User Story 2 - Director connects the organisation's Mollie account (Priority: P1)

A director wants to offer online payment to parents. They connect their organisation's own
Mollie account so payments land directly in their own bank account.

**Why this priority**: Online payment (User Story 1) cannot function for any organisation until
this onboarding exists — it's a co-requisite for P1, listed second only because it's a one-time
setup action rather than the recurring parent-facing flow.

**Independent Test**: Can be fully tested by a director opening the organisation Settings page,
completing the Mollie OAuth flow (sandbox/test mode), and confirming the connected status
persists and survives a page reload — independent of any actual invoice payment.

**Acceptance Scenarios**:

1. **Given** a director with no connected payment provider, **When** they open the organisation
   Settings page, **Then** they see a "Connect Mollie" call to action and no payment-related
   data.
2. **Given** a director mid-OAuth-flow, **When** they complete Mollie's hosted onboarding,
   **Then** they are returned to the app showing a connected status with the linked account
   identified.
3. **Given** an OAuth flow that fails or is cancelled, **When** the director returns to the app,
   **Then** they see a clear error state with a retry action, not a silent failure or a false
   "connected" state.
4. **Given** a connected organisation, **When** the director chooses to disconnect, **Then** the
   connected status is cleared and "Pay now" stops being offered on that organisation's
   invoices (falling back to bank transfer only, per User Story 1 scenario 3).

---

### User Story 3 - Parent receives an automatic payment reminder (Priority: P2)

A parent has an unpaid invoice past its due date. Instead of the director manually following up,
the system sends a reminder automatically.

**Why this priority**: Valuable and explicitly requested for competitive parity, but the
platform is functional without it (a director can still see overdue invoices in the existing
014 list and follow up manually) — it's an efficiency layer on top of P1, not a blocker for it.

**Independent Test**: Can be fully tested by seeding a `Sent` invoice with a due date in the
past for a location with reminders enabled, running the reminder job, and confirming exactly one
reminder notification is generated (and that a second run the same day doesn't duplicate it) —
independent of the payment/OAuth flows.

**Acceptance Scenarios**:

1. **Given** a `Sent` invoice past its due date at a location with reminders enabled, **When**
   the reminder job runs, **Then** the invoice's linked contacts receive a reminder notification.
2. **Given** an invoice that already received a reminder today, **When** the reminder job runs
   again the same day, **Then** no duplicate reminder is sent.
3. **Given** a location with reminders disabled, **When** the reminder job runs, **Then** no
   reminder is sent for that location's overdue invoices.
4. **Given** an invoice that transitions to `Paid` (by any path), **When** the reminder job next
   runs, **Then** no further reminder is sent for that invoice.
5. **Given** an invoice that has already received 3 automatic reminders, **When** the reminder
   job runs again, **Then** no further automatic reminder is sent for that invoice (the director
   may still follow up manually via 014's existing tools).

---

### User Story 4 - Parent receives an automatic payment receipt (Priority: P2)

When an invoice is marked paid — whether by the parent paying online or the director marking it
paid manually — the parent automatically receives a betalingsbewijs.

**Why this priority**: A natural companion to both online payment and 014's existing manual
mark-paid flow; not blocking either, but expected competitive parity (Bitcare already offers
this).

**Independent Test**: Can be fully tested by marking an invoice paid (via either path) and
confirming a receipt document is generated and reachable by the parent, independent of how the
invoice became paid.

**Acceptance Scenarios**:

1. **Given** an invoice paid via the online payment webhook, **When** the transition completes,
   **Then** the parent's linked contacts are notified and a receipt becomes available to view or
   download.
2. **Given** an invoice marked paid manually by a director (014's existing flow), **When** the
   transition completes, **Then** the same receipt generation and notification occurs — receipts
   are not exclusive to online payment.
3. **Given** a receipt already generated for an invoice, **When** queried again, **Then** the
   same receipt is returned (not regenerated with a new document each time).

---

### Edge Cases

- What happens when Mollie's webhook is delivered more than once for the same payment? The
  transition to `Paid` must be idempotent — a second delivery is a no-op, not a duplicate
  receipt or a duplicate reminder-cancellation side effect.
- What happens when a webhook payload's claimed tenant/invoice doesn't correspond to any
  invoice this system generated a payment reference for? The request must be rejected without
  revealing which part of the payload was wrong (avoid a tenant-enumeration oracle) — same
  posture as 014's OGM reference being system-generated and never client-supplied.
- What happens when a parent pays exactly at the moment a director manually marks the same
  invoice paid? Both paths converge on the same one-way `Sent → Paid` transition (014's existing
  guard already rejects a second transition from `Paid`) — only one wins, no double-receipt.
- What happens when Mollie's OAuth connection is revoked from Mollie's side (not from this app),
  or its access token has simply expired and refresh fails? Both produce the identical
  "reconnect" outcome — the next payment-link creation attempt must fail gracefully and surface
  a reconnect state to the director, not a silent broken link shown to the parent; this system
  does not need to distinguish revocation from expiry to the director, only detect that the
  connection is no longer usable.
- What happens if a director changes a location's reminder delay/cadence after some automatic
  reminders have already been sent for an invoice? The 3-reminder cap (FR-013) is absolute per
  invoice — a settings change never resets `ReminderCount`, only affects the timing of any
  remaining reminders up to the existing cap.
- What happens to an invoice's reminder progress when a director regenerates it (014's existing
  regenerate flow, valid on `Draft`/`Sent`)? `ReminderCount`/`LastReminderSentAt` are unaffected
  by regeneration — consistent with 014's own rule that `DueDate` is never recomputed on
  regenerate (014 FR-011), so the invoice's overdue timeline, and therefore its reminder
  progress, does not restart either.
- What happens when a reminder is due on an invoice that has no linked contact with a push token
  or account? Mirrors 014's `InvoiceNotificationService` precedent — an in-app `Notification` row
  is still created where a `TenantUserId` exists; a contact with neither is simply skipped, not
  treated as an error.
- What happens when an organisation disconnects Mollie while an invoice's payment link is still
  outstanding (parent has the link open)? The in-flight payment on Mollie's side still completes
  normally (Mollie processes payments independently of the app-level connection flag) and the
  webhook still resolves and marks it paid; only *new* link creation is blocked once disconnected.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Directors MUST be able to connect their organisation's Mollie account via an
  OAuth flow initiated from director web, without this system collecting or storing any KYC/PII
  document beyond what Mollie's own hosted onboarding collects.
- **FR-002**: The system MUST persist the connected Mollie account's identity and OAuth tokens
  per organisation, encrypted at rest, and expose only a connected/disconnected status (never
  the raw tokens) to any client.
- **FR-003**: Directors MUST be able to disconnect a previously connected Mollie account, after
  which no new payment link may be created for that organisation's invoices.
- **FR-004**: For a `Sent` invoice belonging to an organisation with a connected Mollie account,
  the system MUST offer parents a "Pay now" action that creates a Mollie-hosted payment for the
  invoice's exact outstanding amount and redirects the parent to it. If an active (pending/open,
  not expired/failed/cancelled) payment already exists for that invoice, "Pay now" MUST reuse it
  rather than create a second one — at most one active payment per invoice at any time.
- **FR-005**: For a `Sent` invoice belonging to an organisation with NO connected Mollie account,
  the system MUST NOT offer "Pay now" — the parent sees only the existing 014 bank-transfer
  instructions (OGM reference), unchanged.
- **FR-006**: The system MUST expose a webhook endpoint that receives Mollie payment-status
  events, is exempt from the standard JWT-based tenant resolution (`TenantExempt`, per
  `TenantMiddleware`'s existing pattern), and resolves the tenant and invoice solely from a
  payment reference the system itself generated when creating the payment link — never from any
  tenant/organisation identifier present in the inbound payload.
- **FR-007**: On receiving a webhook event, the system MUST independently verify the payment's
  status directly with Mollie's API before acting on it — the webhook payload is a trigger to
  check, not a trusted source of the actual payment state.
- **FR-008**: Marking an invoice paid via the online-payment webhook MUST use the exact same
  one-way `Sent → Paid` transition 014 already enforces (no transition from `Paid`, no "unmark"),
  so an invoice paid online and an invoice marked paid manually are indistinguishable in every
  downstream flow (reporting, receipts, audit trail).
- **FR-009**: Webhook processing MUST be idempotent — repeated delivery of the same payment
  event MUST NOT re-trigger the paid transition, duplicate a receipt, or duplicate a
  notification once the invoice is already `Paid`.
- **FR-010**: When a parent returns to the app after completing payment but before the webhook
  has been processed, the invoice screen MUST show a distinct "confirming payment" state rather
  than either the prior unpaid state or a false paid state, resolving automatically once the
  webhook lands.
- **FR-011**: The system MUST record any PSP fee associated with a payment separately from the
  invoice; the invoice's `TotalCents` (014) MUST NEVER be mutated by a payment or its fee.
- **FR-012**: Directors MUST be able to configure, per location, whether automatic payment
  reminders are enabled, the delay (in days) after the due date before the first reminder, and
  the reminder cadence (repeat interval) thereafter — mirroring the per-location numeric-setting
  pattern already established by `Location.InvoiceDueDays` (014) and `ReservationNoticeHours`
  (013f). Reminders default to disabled; when first enabled for a location, the delay and
  cadence pre-fill to 3 days and 7 days respectively, both director-adjustable.
- **FR-013**: A recurring system process MUST identify `Sent` invoices past their due date at
  locations with reminders enabled and send at most one reminder per invoice per configured
  interval — never more than one reminder for the same invoice on the same day, never more than
  3 automatic reminders total for a given invoice, and never any reminder once the invoice is
  `Paid`.
- **FR-014**: Reminder notifications MUST reach every contact linked to the child on the
  invoice, using the same in-app `Notification` + best-effort push pattern as
  `InvoiceNotificationService` (014), with dedicated i18n copy distinct from the "invoice sent"
  notification.
- **FR-015**: When an invoice transitions to `Paid` — by either the online-payment webhook or
  014's existing manual mark-paid action — the system MUST generate a betalingsbewijs (payment
  receipt) and notify the invoice's linked contacts that it is available. The receipt is a
  payment confirmation only (KDV identity, child/parent name, invoice reference, amount paid,
  date paid) — not a tax/legal document; fiscal attestations remain feature 015's separate,
  unstarted scope. It MUST NOT introduce any personal data beyond what 014's existing invoice
  PDF already displays — no new PII field is added for this feature.
- **FR-016**: A betalingsbewijs's substantive content (amounts, names, dates, references) MUST
  be deterministic per invoice — viewing or downloading it multiple times MUST always reflect
  the same underlying data for the same paid invoice, never a different amount/date/reference
  on repeat access. (This does not require byte-identical output — on-demand PDF rendering may
  embed a fresh generation timestamp each time, matching 014's existing invoice PDF.)
- **FR-017**: A betalingsbewijs MUST be viewable and downloadable by the parent from the parent
  app, following the same on-demand PDF rendering pattern already established for invoice PDFs
  (014) — available only once the invoice is `Paid`.
- **FR-018**: The payment-provider integration MUST be implemented behind an abstraction (an
  `IPaymentProvider`-shaped seam) such that no Mollie-specific type or API shape is referenced
  outside that boundary — a future PSP (Stripe, POM) must be addable without changing calling
  code.
- **FR-019**: All monetary amounts introduced by this feature (payment amounts, PSP fees) MUST
  be stored and handled in cents, matching 014's existing convention.
- **FR-020**: All new user-facing strings (reminder notifications, receipt content, connection
  status, payment states) MUST use i18n keys with NL/FR/EN translations, matching every prior
  feature's convention.
- **FR-021**: This feature MUST NOT alter 014's billable-day computation, invoice PDF content,
  or the existing manual mark-paid flow's availability — online payment is an additional path to
  the same `Paid` state, not a replacement.

### Key Entities

- **Payment Provider Connection**: Represents one organisation's connected Mollie sub-merchant
  account — the Mollie account reference, encrypted OAuth tokens, and a connected/disconnected
  status. One per organisation. Lives outside any single tenant schema (needed for cross-tenant
  webhook resolution — see Technical Requirements).
- **Payment**: Represents one payment attempt against an invoice — provider payment reference,
  status, amount, PSP fee (recorded separately from the invoice total), and timestamps.
  Tenant-scoped, linked to exactly one `Invoice` (014).
- **Reminder Settings**: Per-location configuration — enabled flag, delay in days after due
  date, and repeat cadence. Extends `Location` (014) the same way `InvoiceDueDays` does.
- **Betalingsbewijs (Receipt)**: The payment-confirmation document for a paid invoice, rendered
  on demand from the invoice's `Paid`-state fields (matching 014's existing invoice-PDF pattern
  — not a separately stored/persisted entity). Content: KDV identity, child/parent name, invoice
  reference, amount paid, date paid — distinct from and not a substitute for feature 015's
  future fiscal attestation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A parent can complete an online payment for an invoice in under 3 taps from the
  invoice screen and under 2 minutes end to end (including the hosted payment page).
- **SC-002**: A director can connect their organisation's Mollie account in a single guided flow
  with zero manual document upload inside this app.
- **SC-003**: 100% of overdue invoices at a location with reminders enabled receive at least one
  reminder without any director action.
- **SC-004**: 100% of invoices transitioning to `Paid` (any path) produce exactly one
  betalingsbewijs, reachable by the parent within the same session as the payment confirmation.
- **SC-005**: An organisation that never connects Mollie experiences zero behavioral change to
  its existing invoice generation, sending, or manual payment-tracking flow.
- **SC-006**: No webhook delivery, including duplicates or forged payloads, ever changes an
  invoice belonging to a different organisation than the one that generated the payment.

## Assumptions

- Mollie Connect for Platforms (OAuth-based sub-merchant onboarding with payment splitting) is
  the PSP for this feature, per the 2026-07-16 product-owner decision recorded in
  `BACKLOG.md`'s feature 014a prompt block — not re-litigated here.
- No dedicated bulk-email infrastructure exists yet (feature 020 is unstarted); reminders and
  receipt notifications use the existing in-app `Notification` + best-effort push pattern
  (`InvoiceNotificationService`, feature 014), the same fallback the 014a prompt block itself
  specifies.
- Partial payments and payment plans are out of scope for this feature (explicitly deferred per
  the 014a prompt block) — a payment is expected to cover an invoice's full outstanding amount.
- SEPA direct debit (026) and CODA bank-statement matching (025) are separate, unbuilt features
  and out of scope here.
- No recurring background-job runner exists yet in this codebase (only manually-invoked CLI
  commands, e.g. `migrate-tenants`, `backfill-growth-check`); the specific mechanism for running
  the reminder job on a recurring schedule is a planning-phase technical decision, not a product
  decision, and does not change this spec's functional requirements.
- Reminder cadence defaults to disabled per location (opt-in), so a location that never
  configures reminder settings sees no behavior change — consistent with `InvoiceDueDays`'s
  existing "sensible default, zero setup required" precedent, but reminders themselves are a new
  parent-facing behavior a director should consciously enable.
