# Quickstart: Monthly Menu Variants

Validation scenarios proving the feature works end-to-end, once implemented. Assumes a running
local stack (`backend`, `web`, `parent-mobile`) against a tenant seeded with a location, a
director account, and at least two children (with a linked parent) enrolled there — reuse
013e/013i's existing quickstart prerequisites. Applying the tenant migration is required
(`dotnet ef database update --context TenantDbContext`, or the generated SQL script, per this
repo's no-auto-migrate convention).

## Scenario 1 — Base menu is unaffected by this feature (FR-012/SC-003)

1. Without touching any location's variant settings, author and publish a base menu exactly as
   in 013e's own quickstart.
2. Confirm the parent app shows it exactly as before — no variant labeling, no new UI.

**Expected outcome**: a location that never configures a variant is indistinguishable from
pre-013j behavior.

## Scenario 2 — Director configures and authors a variant (User Stories 1 & 2)

1. As director, open the location's settings and enable "Vegetarian" and "Halal", in that
   priority order. Save.
2. In the Menu section, confirm the variant selector now offers Base / Vegetarian / Halal.
3. Select Vegetarian, fill in a full month via the day-grid (or CSV import, 013i), Save, Publish.
4. Confirm the base menu for the same location/month is completely unchanged.
5. Attempt to `PUT` a menu for a `DietaryType` not enabled for this location (e.g. `vegan`, via a
   direct API call) — confirm it's rejected.

**Expected outcome**: variant authoring is independent of the base menu and gated by the
location's enabled-variant configuration.

## Scenario 3 — Parent sees the resolved menu per child (User Story 3)

1. Set one child's `MealPreference.DietaryType` to include Vegetarian (013d's existing meal
   preference UI). Leave a second child (same location) with no dietary preference set.
2. As the linked parent, open the Menu tab.
3. Confirm the first child's section shows the Vegetarian variant, clearly labeled; the second
   child's section shows the base menu.
4. Set the first child's `DietaryType` to include both Vegetarian and Halal. Reload.
5. Confirm the child's section now shows Halal (the higher-priority match from Scenario 2's
   configured order), not Vegetarian.

**Expected outcome**: resolution is automatic, per-child, and respects the director's priority
order.

## Scenario 4 — Draft variant falls back correctly (Edge Case)

1. Un-publish the Vegetarian variant from Scenario 2 (leave it as a draft).
2. Reload the parent app for the child who qualifies for Vegetarian.

**Expected outcome**: that child's section falls back to the base menu (or Halal, if that's still
published and the child also qualifies) — never shows draft content.
