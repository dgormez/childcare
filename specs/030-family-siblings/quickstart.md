# Quickstart: Family Siblings

Validation scenarios proving the feature works end-to-end, once implemented. Assumes a tenant
with a location, a director account, and a parent Contact linked (as primary, `Mother`
relationship) to two children ("Emma" and "Lucas") enrolled at the same location with active
contracts (Emma's contract starting a month before Lucas's).

## Scenario 1 — Parent reports both siblings absent in one action (User Story 1)

1. As the linked parent, open the absence-request form. Confirm an "apply to all my children"
   option is shown alongside the per-child picker (not shown if the parent only had one child).
2. Select "all children", pick a date, submit.
3. Confirm two reservation records now exist ("My requests"), one per child, each independently
   visible to the director's approval queue.
4. As director, disable absence requests (013f) for this location, then repeat step 2 as the
   parent.
5. Confirm the submission response indicates both children were skipped with the disabled-type
   reason, and no reservation records were created.

**Expected outcome**: one action creates independent per-child reservations; a location-level
block applies per child, never silently to the whole household.

## Scenario 2 — Director configures sibling discount (User Story 2)

1. As director, open the location's invoicing settings, set sibling discount to 10%, save.
2. Generate invoices for the current month.
3. Confirm Emma's invoice (earlier contract start) shows no discount line, and Lucas's invoice
   shows a distinct, labeled discount line item reducing his total by 10% of his subtotal.
4. Reset the discount to 0%, regenerate — confirm both invoices return to full price with no
   discount line (still-Draft invoices only, per 014's existing regenerate rule).

**Expected outcome**: discount applies automatically to the correct sibling, is clearly labeled,
and a location that never opts in sees no change (verify against a second, unconfigured
location's invoices in the same run).

## Scenario 3 — Director enables family invoice bundling (User Story 3)

1. As director, enable family invoice bundling on the location, keep the 10% sibling discount
   from Scenario 2.
2. Generate invoices for the month.
3. Confirm exactly one combined invoice/PDF is produced for Emma+Lucas, showing both children's
   line items (including Lucas's discount) and one combined total.
4. As the parent, confirm the invoice list shows one entry for this period, not two, and the
   combined PDF downloads correctly.
5. Mark the combined invoice paid (director or via a test payment) — confirm both underlying
   per-child invoices transition to Paid together.
6. Disable bundling, generate next month's invoices — confirm they're separate again per child.

**Expected outcome**: bundling is a presentation/payment grouping only — disabling it later
doesn't corrupt or merge historical per-child invoice data.

## Scenario 4 — Director manages a child's contacts, avoids a duplicate (User Story 4)

1. As director, open Emma's profile, confirm the new Contacts section lists the linked mother
   with relationship and primary flag.
2. Open Lucas's profile, start adding a new contact using the same mother's email address.
3. Confirm the UI surfaces the existing contact as a suggested match instead of silently letting
   a duplicate be created; link the existing contact.
4. Confirm Lucas's Contacts section now shows the same mother, and Emma's is unaffected.
5. Add a genuinely new contact (a grandparent, relationship "Other") to Lucas — confirm it saves
   and appears.

**Expected outcome**: no duplicate `Contact` row is created for the same real person across
siblings; the new `Other`/foster-parent relationship options are selectable.

## Scenario 5 — A departed sibling's history remains reachable (User Story 5)

1. As director, deactivate Emma (contract ended), leaving Lucas active.
2. As the parent, confirm the default child view now shows only Lucas.
3. Open the "previous children" view, confirm Emma appears with her enrollment period.
4. Open Emma from that view, confirm her past daily reports and invoices are still viewable
   (read-only — no new event/reservation actions offered for her).
5. As a parent with zero deactivated children (a different test account), confirm no "previous
   children" entry point is shown at all.

**Expected outcome**: an active/deactivated split keeps the daily view uncluttered without
losing access to a departed child's history.
