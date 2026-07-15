# Quickstart: Invoicing

Validation scenarios proving the feature works end-to-end, once implemented. Assumes a running
local stack (`backend`, `web`, `parent-mobile`) against a tenant seeded with a location, a
director account, at least one child with an active contract and a linked parent, and some
attendance history for that child (mix of present/absent/closure days) — reuse 007/009/010/011's
existing quickstart prerequisites.

## Scenario 1 — Director generates and reviews a month's invoices (User Story 1)

1. As director, open the Invoicing settings tab for the location and confirm it, erkenningsnummer,
   bank account number, and invoice-due-days (default 14) can be set and saved.
2. Open the Invoices section, select the location and a month with existing attendance history,
   click "Generate invoices".
3. Confirm one draft invoice appears per child with a contract active that month, with a
   line-item breakdown matching present + unjustified-absent − closure days at the contract's
   daily rate.
4. Click "Generate invoices" again for the same location/month — confirm no duplicate invoices
   are created.

**Expected outcome**: draft invoices exist, one per (child, contract, location, month), with a
correct computed breakdown.

## Scenario 2 — Director adds an extra charge, sends, and tracks payment (User Story 2)

1. Open a draft invoice, add an extra charge ("Registration fee", 25.00), confirm the total
   updates.
2. Send the invoice. Confirm its status becomes `sent`, it now has an OGM reference and a due
   date, and downloading its PDF shows every required field (KDV name/address/KBO/
   erkenningsnummer if set, parent/child name, period, breakdown, total, due date, OGM
   reference, bank account number).
3. As the linked parent, open the Invoices section in the parent app and confirm the invoice now
   appears with the correct amount and a plain-language "Awaiting payment" status.
4. Back as director, record payment with today's date. Confirm status becomes `paid` and the
   parent's view now shows "Paid".

**Expected outcome**: send makes an invoice parent-visible with a valid payment reference;
marking paid updates both the director and parent views.

## Scenario 3 — Overdue display and paid-invoice immutability (Edge Cases)

1. Send an invoice with a due date in the past (e.g. temporarily set a location's
   `invoiceDueDays` to a negative test value, or use a backdated attendance month) and confirm
   it appears as "Overdue" in the director list without any manual status change.
2. Attempt to regenerate a `paid` invoice (from Scenario 2) — confirm the system rejects it and
   the invoice's line items are unchanged.

**Expected outcome**: overdue is purely computed from `status`+`dueDate`; a paid invoice can
never be altered.

## Scenario 4 — Split-location child gets two independent invoices (Edge Cases)

1. Give one child active contracts at two different locations for the same month, with
   different attendance history at each.
2. Generate invoices for both locations for that month.
3. Confirm two separate invoices exist for that child — one per location — each reflecting only
   that location's own attendance/closure data, and confirm the parent's invoice list shows both,
   clearly attributed to the correct location.

**Expected outcome**: invoicing is fully independent per location for a child with
simultaneous contracts at more than one (constitution Principle II's split-location scenario).
