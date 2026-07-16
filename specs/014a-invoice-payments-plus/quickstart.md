# Quickstart: Invoice Payments Plus

Validation scenarios proving the feature works end-to-end, once implemented. Assumes 014's own
quickstart prerequisites (a tenant with a location, a director account, a child with an active
contract and linked parent) plus a Mollie **test-mode** account for OAuth/payment testing —
never exercise these scenarios against Mollie live-mode credentials.

## Scenario 1 — Director connects Mollie (User Story 2)

1. As director, open Settings, confirm the payment-connection section shows "not connected".
2. Click "Connect Mollie", complete Mollie's test-mode OAuth consent screen, and confirm the
   redirect back into the app shows a connected status with the test account's label.
3. Reload the page — confirm the connected status persists.
4. Click disconnect — confirm the status reverts to "not connected" and a subsequent "Pay now"
   attempt (Scenario 2) is no longer offered for this organisation's invoices.
5. Reconnect using the same Mollie test account — confirm the connection succeeds again (the
   "reconnect" edge case from spec.md).

**Expected outcome**: connect/disconnect/reconnect all work via Mollie's own hosted OAuth flow,
with no custom KYC/document-upload UI in this app.

## Scenario 2 — Parent pays an invoice online (User Story 1)

Prerequisite: Scenario 1 completed (organisation has a connected test-mode Mollie account) and
a `Sent` invoice exists for a child (reuse 014's own Scenario 2 to produce one).

1. As the linked parent, open the invoice and confirm a "Pay now" action is shown.
2. Tap "Pay now" — confirm redirection to a Mollie-hosted test checkout page pre-filled with the
   invoice's exact outstanding amount.
3. Complete a test payment (Mollie's test mode provides a status-selection page — choose "paid").
4. Return to the app — confirm a brief "confirming payment" state is shown, resolving to "Paid"
   once the webhook lands (may require a moment; the webhook is asynchronous).
5. Confirm the invoice is now `Paid` in both the parent and director views, `paidAt` is set, and
   a betalingsbewijs is available to view/download from the parent app.
6. Tap "Pay now" again on a *different* unpaid invoice, abandon the Mollie checkout page instead
   of completing it, and return to the app — confirm the invoice remains unpaid and "Pay now" is
   still offered.

**Expected outcome**: online payment reaches `Paid` through the same one-way transition 014
already enforces, with a receipt available immediately after.

## Scenario 3 — Idempotent webhook and payment-link reuse (Edge Cases, research.md R2/R6)

1. Repeat Scenario 2 steps 1–3 for a new invoice, then manually re-deliver the same webhook call
   (e.g. via Mollie's dashboard "resend webhook" or a direct duplicate `curl` to the recorded
   `paymentReference` URL). Confirm the invoice is still `Paid` exactly once — no duplicate
   receipt, no duplicate notification.
2. Before completing payment, tap "Pay now" twice on the same unpaid invoice from two different
   sessions — confirm both redirect to the *same* Mollie checkout URL (the existing open
   `Payment` row is reused, not duplicated).
3. `curl` the webhook path with a `paymentReference` that doesn't correspond to any created
   payment — confirm a generic response with no information about why it failed, and confirm no
   invoice anywhere changes state.

**Expected outcome**: webhook processing and payment-link creation are both idempotent; a
forged/unresolvable reference has zero effect.

## Scenario 4 — Automatic payment reminders (User Story 3)

1. Enable payment reminders for a location (Settings → the location's Invoicing tab), using the
   default 3-day delay / 7-day cadence.
2. Seed (or wait for, via a backdated due date) a `Sent` invoice past its due date at that
   location.
3. Run the reminder CLI command (research.md R4) manually in the local/dev environment.
4. Confirm the invoice's linked contacts received exactly one reminder notification, and
   `reminderCount`/`lastReminderSentAt` are set on the invoice.
5. Run the command again immediately — confirm no duplicate reminder is sent (cadence not yet
   elapsed).
6. Mark the invoice paid — run the command again — confirm no further reminder is sent.

**Expected outcome**: reminders are cadence-respecting, capped, and stop the moment an invoice
is paid, with zero director action required beyond the initial settings toggle.

## Scenario 5 — An organisation that never connects Mollie is unaffected (SC-005)

1. For a *different*, never-connected organisation, generate and send an invoice exactly as in
   014's own quickstart.
2. Confirm no "Pay now" action appears anywhere, the bank-transfer/OGM instructions are
   unchanged from 014, and manually marking the invoice paid still works exactly as before —
   including receipt generation (FR-015 applies to the manual path too).

**Expected outcome**: zero behavioral change for an organisation that opts out of online
payment entirely.
