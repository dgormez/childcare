# Quickstart: Fiscal Attestations

Validation scenarios proving the feature works end-to-end, once implemented. Assumes 014's own
quickstart prerequisites (a tenant with a location, a director account, a child with an active
contract and a linked parent) plus at least one `Paid` invoice for the relevant tax year (reuse
014's `MarkInvoicePaidCommand` flow to produce one, or 014a's online-payment flow).

## Scenario 1 — Director bulk-generates a tax year (User Story 1)

1. Seed three children for tax year 2026: child A with a full year of `Paid` invoices at one
   daily rate; child B with `Paid` invoices at two different daily rates (a mid-year contract
   amendment, per 007's `PreviousContractId` chain); child C with zero `Paid` invoices for 2026.
2. As director, open the Fiscal Attestations screen, select tax year 2026, and trigger bulk
   generation.
3. Confirm child A's attestation shows one period covering the full year.
4. Confirm child B's attestation shows two periods, each with the correct start/end date, day
   count, amount, and daily rate matching their two rate windows.
5. Confirm child C does not appear as a generated row — the list shows "no paid invoices this
   year" for them, not an error or a zero-amount attestation.
6. Confirm every generated child's linked parent contact received a notification (FR-016).

**Expected outcome**: bulk generation produces one accurate multi-period attestation per eligible
child, skips ineligible children cleanly, and notifies parents.

## Scenario 2 — Parent downloads their child's attestation (User Story 2)

Prerequisite: Scenario 1 completed for child A.

1. As child A's linked parent, open the parent app's Fiscal Attestations area.
2. Confirm the 2026 attestation is listed and downloads successfully as a PDF.
3. Open the PDF and confirm: KDV name/address/KBO/erkenningsnummer are correct, the parent's and
   child's names and the child's date of birth are correct, the period breakdown and total match
   what the director saw in Scenario 1, and the NRN field is present but **blank** — never
   pre-filled.
4. As a parent with no attestation generated yet for a different tax year (e.g. 2025), confirm a
   clear "not available yet" state is shown, not an error.

**Expected outcome**: the parent can self-serve their child's attestation, and it never contains
a pre-filled NRN.

## Scenario 3 — Director corrects a single attestation (User Story 3)

Prerequisite: Scenario 1 completed for child A.

1. Mark an additional invoice `Paid` for child A within tax year 2026 (simulating a late
   correction discovered after the initial bulk run).
2. As director, regenerate child A's 2026 attestation individually (not a full bulk re-run).
3. Confirm the attestation's total amount and periods now reflect the additional paid invoice,
   `generatedAt` is updated, and no duplicate row/attestation exists for child A/2026.
4. Confirm child B's and child C's attestations from Scenario 1 are completely unaffected.
5. As child A's parent, re-download the attestation — confirm the corrected version is served,
   not a stale cached one — and confirm a second notification was sent for the regeneration.

**Expected outcome**: a single correction never disturbs the rest of the year's batch, and the
parent always sees the latest version.

## Scenario 4 — Re-running bulk generation doesn't clobber existing attestations (Edge Cases,
FR-009)

Prerequisite: Scenario 3 completed (child A has a regenerated attestation with a corrected
total).

1. As director, trigger bulk generation for tax year 2026 again.
2. Confirm child A's attestation (already existing) is left completely untouched — same
   `generatedAt`/total as after Scenario 3, not silently reset to the Scenario-1 amount.
3. Seed a fourth child, D, with `Paid` invoices for 2026 that didn't exist at the time of
   Scenario 1's run, and confirm this bulk re-run generates an attestation for D (the "only
   children without an existing row" rule, not "only children that existed at first run").

**Expected outcome**: bulk re-runs are additive-only for new eligible children; correcting an
existing attestation always requires the explicit per-child regenerate action.

## Scenario 5 — Multi-location split (Edge Cases, research.md R6)

1. Seed a child who has `Paid` invoices at two different locations within tax year 2026 (a
   mid-year transfer).
2. Run bulk generation for 2026.
3. Confirm **two** attestations exist for this child — one per location — each showing only that
   location's periods, name, address, and erkenningsnummer, and each total summing to only that
   location's paid invoices (not the combined total across both).

**Expected outcome**: a child's tax certificate is always correctly attributed per childcare
site, matching the site-specific licence number requirement.
