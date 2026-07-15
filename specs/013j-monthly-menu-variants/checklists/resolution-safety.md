# Resolution & Data-Safety Requirements Quality Checklist: Monthly Menu Variants

**Purpose**: Validate `spec.md`'s requirements in the highest-risk areas for this feature —
resolution-algorithm correctness (priority order, multi-match, fallback), the base-menu
backward-compatibility guarantee, and settings-change safety — before implementation.
**Created**: 2026-07-14
**Feature**: [spec.md](../spec.md)

**Note**: This checklist tests whether the *requirements* are complete, clear, and unambiguous —
not whether an implementation works. `[Gap]` marks a missing requirement, `[Ambiguity]` marks an
underspecified one. All 7 findings below were fixed directly in `spec.md` (plus `data-model.md`,
`contracts/monthly-menu-variants-api.md`, `tasks.md`) rather than deferred, per this repo's
standing rule.

## Requirement Clarity — Resolution Algorithm

- [x] CHK001 Does FR-008 state that `DietaryType` matching is exact equality only, with no semantic hierarchy between types (e.g. a child marked Vegan does NOT automatically match an enabled Vegetarian variant, even though vegan is a stricter subset in dietary practice)? [Ambiguity, Spec §FR-008] — **Resolved**: FR-008 now states this explicitly; added test task T053.
- [x] CHK002 Is it specified whether "priority order" position is preserved when a director disables then later re-enables the same `DietaryType`, or whether it resets to the end of the list? [Ambiguity, Spec §FR-002] — **Resolved**: FR-002 now specifies append-at-end on re-enable; added US1 acceptance scenario 5 and test task T048.

## Requirement Completeness — Settings-Change Safety

- [x] CHK003 Is there a requirement covering what happens when a director disables a `DietaryType` that currently has a *published* variant menu — does any child actively seeing it get silently switched to a fallback with no director-facing warning at the moment of disabling? [Gap, Spec §FR-007] — **Resolved**: new FR-014 requires an explicit confirmation warning; new `menuVariantsWithPublishedContent` response field (data-model.md/contracts.md) and tasks T049-T052.
- [x] CHK004 Does the spec require rejecting duplicate `DietaryType` entries within a single `MenuVariantPriorityOrder` at the requirements level (only `contracts/monthly-menu-variants-api.md`'s Validation section currently states this, not a spec.md FR)? [Gap, Spec §FR-002] — **Resolved**: FR-002 now states this explicitly.
- [x] CHK005 Is it specified whether a variant's `PublishedAt` state survives a disable-then-re-enable cycle (FR-007 says content is retained, but doesn't explicitly say the *publish state* is retained rather than reset to draft)? [Ambiguity, Spec §FR-007] — **Resolved**: FR-007 now explicitly covers publish-state retention.

## Edge Case Coverage — Multi-Location Children

- [x] CHK006 Is there an edge case covering a child holding active contracts at two different locations simultaneously (the constitution's own split-location scenario, Principle II) — does each location resolve that child's variant independently using that location's own `MenuVariantPriorityOrder`? [Gap, Spec §Edge Cases] — **Resolved**: new Edge Cases bullet; added test task T054.

## Acceptance Criteria Quality

- [x] CHK007 Can SC-004 ("zero cross-contamination between siblings' dietary results") be objectively verified as written, or does it need a more concrete, testable proxy tied to a specific scenario? [Measurability, Spec §SC-004] — **Resolved**: SC-004 rewritten around a concrete single-load, single-child-change scenario.

## Notes

- CHK003 was the highest-impact finding: disabling a variant was previously a silent, unguarded
  action with a real parent-facing consequence (a family could stop seeing their child's
  dietary-appropriate menu with no signal to the director that anyone was relying on it).
- CHK001 mattered because "Vegan implies Vegetarian" is a plausible, defensible real-world
  assumption an implementer could reasonably build without the spec explicitly ruling it out —
  and the confirmed product-owner decision (BACKLOG.md's `### 013j` note) was exact-match only.
- All 7 items are now resolved directly in the requirements artifacts; none were deferred as
  follow-up debt.
