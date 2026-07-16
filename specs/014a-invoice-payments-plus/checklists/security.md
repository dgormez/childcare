# Security & Regulatory Requirements Checklist: Invoice Payments Plus

**Purpose**: Validate that the spec's requirements around payment processing, OAuth credential
storage, the public webhook, and cross-tenant public-schema resolution are complete, clear, and
consistent enough to plan/implement safely — this codebase's first feature touching all four.
**Created**: 2026-07-16
**Feature**: [spec.md](../spec.md)

## Cross-Tenant Resolution & Isolation

- [x] CHK001 Is the mechanism for resolving tenant/invoice from an inbound webhook explicitly
  required to avoid trusting any client-supplied identifier? [Completeness, Spec §FR-006]
- [x] CHK002 Is "never trust the payload" scoped precisely — payload used as a trigger vs.
  payload used as the source of truth for payment state? [Clarity, Spec §FR-007]
- [x] CHK003 Does the spec define the required behavior when a webhook payload references a
  tenant/invoice inconsistent with what the system's own reference resolves to, beyond "reject"?
  [Edge Case, Spec Edge Cases]
- [x] CHK004 Is there a requirement preventing a webhook response from revealing *which* part of
  an invalid request was wrong (tenant-enumeration-oracle resistance)? [Completeness, Spec Edge
  Cases]
- [x] CHK005 Are requirements consistent between the general tenant-isolation expectation and
  this feature's specific cross-schema resolution need — i.e., does the spec acknowledge where
  this feature's data model necessarily differs from single-tenant-schema storage, rather than
  silently contradicting it? [Consistency, Spec §Technical Requirements]

## OAuth & Credential Storage

- [x] CHK006 Are requirements defined for what happens to in-flight payment links if OAuth
  tokens expire or are revoked mid-session? [Edge Case, Spec Edge Cases]
- [x] CHK007 Is "encrypted at rest" specific enough to be verifiable (what's covered — access
  token, refresh token, both) rather than a vague security adjective? [Clarity, Spec §Technical
  Requirements]
- [x] CHK008 Does the spec require that connection status responses exclude raw credentials,
  not just "not display them by default"? [Measurability, Spec §FR-002]
- [x] CHK009 Are requirements defined for the disconnect action's effect on already-open (not
  yet completed) payment links, distinct from its effect on new link creation? [Coverage, Spec
  Edge Cases]
- [x] CHK010 Is scope/permission granularity for the OAuth connection addressed (e.g., whether
  the app requests broader account access than payment creation strictly needs), or is that
  explicitly deferred to planning? [Gap]

## Payment Integrity & Idempotency

- [x] CHK011 Is "idempotent" defined precisely enough to be testable — same payment event
  delivered N times produces the same end state, not just "no duplicate invoice"? [Measurability,
  Spec §FR-009]
- [x] CHK012 Are requirements defined for the race between a parent completing online payment
  and a director manually marking the same invoice paid at the same time? [Edge Case, Spec Edge
  Cases]
- [x] CHK013 Is the invoice-total-immutability requirement (PSP fees never mutate it) stated as
  a hard constraint with no conditional exception? [Clarity, Spec §FR-011]
- [x] CHK014 Are requirements defined for what a parent sees if they never return to the app
  after completing payment (webhook still arrives, no client polling in progress) — does receipt/
  notification delivery not depend on the client being present? [Coverage, Gap]
- [x] CHK015 Does the spec distinguish a payment that fails/is declined by the PSP from one the
  parent abandons, with different or identical required UI outcomes? [Consistency, Spec §US1
  AC4 / Edge Cases]

## Reminder Cadence & Notification Correctness

- [x] CHK016 Is the reminder cap (3) stated as an absolute maximum regardless of settings
  changes mid-cycle (e.g., a director shortening the cadence after 2 reminders already sent)?
  [Ambiguity, Spec §FR-013]
- [x] CHK017 Are requirements defined for what happens to `ReminderCount`/cadence tracking if an
  invoice is regenerated (014's existing regenerate flow) while reminders are in progress? [Gap]
- [x] CHK018 Is there a requirement that reminder notification content is distinguishable from
  the "invoice sent" notification, to prevent a parent from missing escalation urgency?
  [Completeness, Spec §FR-014]

## Regulatory & Compliance Boundary

- [x] CHK019 Does the spec clearly bound the betalingsbewijs's content to a non-tax-purpose
  confirmation, explicitly excluding NRN/SSIN or other data reserved for feature 015's fiscal
  attestation? [Consistency, Spec §Clarifications / §FR-015]
- [x] CHK020 Is there a requirement that no PII beyond what's already shown on 014's invoice PDF
  is newly introduced by the receipt or reminder content? [Gap]
- [x] CHK021 Are money-handling requirements (cents, no floating point) stated as applying to
  every new monetary field this feature introduces (payment amount, PSP fee), not just the
  invoice total already covered by 014? [Completeness, Spec §FR-019]

## Non-Functional & Failure Modes

- [x] CHK022 Are the specific failure modes of the payment-provider connection (network error
  vs. explicit Mollie-side rejection vs. malformed response) required to produce distinguishable
  outcomes, or is a single generic "connection failed" state sufficient per the spec? [Clarity,
  Spec §UX Requirements]
- [x] CHK023 Is there a requirement covering what a parent experiences if "Pay now" is tapped
  while the organisation's Mollie connection is mid-token-refresh or otherwise transiently
  unavailable? [Edge Case, Gap]
- [x] CHK024 Are the recurring reminder job's own failure/partial-completion requirements
  addressed (e.g., job errors on tenant N of M) — is at-least-once vs. exactly-once delivery
  expected? [Gap]

## Notes

- All items reviewed against the current spec (post-clarification, post-correction for the
  on-demand-receipt-rendering and Settings-navigation fixes).
- CHK006, CHK016, CHK017, CHK020, CHK024 identified genuine gaps and were fixed directly in
  spec.md (new Edge Cases entries for token-expiry/reconnect, reminder-cap persistence across
  settings changes, and reminder-progress across regenerate; a new PII-scope sentence on
  FR-015; a new per-tenant failure-isolation sentence in Technical Requirements) rather than
  left as debt, per this loop's standing rule.
- CHK010 (OAuth scope granularity) and CHK015 (declined vs. abandoned payment UI) were reviewed
  and found to be intentional, correctly-scoped boundaries — not gaps: scope consent is fully
  delegated to Mollie's own hosted flow (spec explicitly avoids building a custom consent UI),
  and unifying declined/abandoned into one "still unpaid, Pay now available" outcome is a
  deliberate UX simplification, not an omission.
- CHK022/CHK023 were reviewed and found already covered by the spec's existing generic
  "clear error state with a retry action" / "payment-link generation failure (retry
  affordance)" language — over-specifying failure-mode taxonomy at the spec level would
  introduce implementation detail the spec should not carry.
- All other items (CHK001–CHK005, CHK007–CHK009, CHK011–CHK014, CHK018, CHK019, CHK021) were
  already adequately addressed by existing requirements — no changes needed.
