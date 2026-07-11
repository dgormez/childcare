# Requirements Quality Checklist: Group Activities

**Purpose**: Validate requirements quality (not implementation) before `/speckit-implement`
**Created**: 2026-07-10
**Feature**: [spec.md](../spec.md)
**Focus**: functional requirement testability, consent/security edge cases, offline behavior
coverage, cross-platform (caregiver/parent/director) consistency

## Requirement Completeness

- [x] CHK001 - Are requirements defined for what happens when a caregiver tries to save an activity with an empty/missing title? [Gap] — resolved: FR-002/data-model.md's `Title` is `NotEmpty` + max 200; client pre-fills from type, so an empty title requires deliberately clearing a pre-filled field. Added as a validator rule (data-model.md Validation), not left implicit.
- [x] CHK002 - Are requirements defined for the caregiver-facing group timeline's pagination/volume behavior as activities accumulate over many days? [Gap] — resolved: spec.md Assumptions scopes the caregiver tablet timeline to "today only," which bounds volume; no historical pagination is in scope for this feature.
- [x] CHK003 - Are requirements defined for what a parent sees when a group activity has a title/description but zero photos (vs. one with photos)? [Spec §User Story 2] — covered: Acceptance Scenario 1 shows title+description+timestamp render regardless of photo count; FR-009 only gates photos specifically.
- [x] CHK004 - Are requirements defined for the director-timeline's behavior when a selected group has no activities/events on the selected date? [Gap] — resolved: this is a standard empty-state case already covered by design-system.md's global empty-state convention (icon + one sentence); no feature-specific requirement needed beyond that shared pattern, so not a spec gap — added a note to UX Requirements' Director Web loading/empty/error line for explicitness.

## Requirement Clarity

- [x] CHK005 - Is "under 30 seconds of active interaction" (SC-001) measured from a defined start/end point? [Clarity, Spec §SC-001] — resolved: clarified in UX Requirements' caregiver tablet Main Flow (tap "Activiteit toevoegen" → save), which bounds the interaction window unambiguously — SC-001 references that same flow, no independent redefinition needed.
- [x] CHK006 - Is "same sync cycle" (SC-002) defined precisely enough to be falsifiable, or is it comparative-only? [Ambiguity, Spec §SC-002] — assessed: deliberately comparative ("no separate/slower path" than an individual child event recorded at the same time) rather than an absolute latency number, consistent with this codebase's existing sync-engine behavior (feature 008) having no published SLA either — not a gap, a reasonable relative success criterion given no prior feature quantifies sync latency in absolute terms.
- [x] CHK007 - Is "current calendar month" (Galerij scope) anchored to a specific timezone given the project's Belgian/Europe context? [Ambiguity, Spec §Assumptions] — resolved: added `Europe/Brussels` as the explicit calendar-month boundary in spec.md Assumptions, matching the existing `BelgianCalendarDay` convention already used by `GetDailySummaryQuery` (research.md R5) — this was an unstated but load-bearing assumption, now made explicit.

## Requirement Consistency

- [x] CHK008 - Are director-delete requirements (FR-011) consistent with the caregiver-tablet's lack of any edit/delete capability (FR-014)? [Consistency, Spec §FR-011, §FR-014] — confirmed consistent: FR-014 explicitly scopes "no editing... beyond director deletion," so FR-011 is the named exception, not a contradiction.
- [x] CHK009 - Do the caregiver-tablet timeline (User Story 1) and director-web timeline (User Story 4) requirements agree on what "the group timeline" contains (events + activities, same merge)? [Consistency, Spec §User Story 1, §User Story 4] — confirmed consistent: both describe "alongside individual events"; research.md R4 documents one shared query behind both surfaces specifically to prevent this drifting.
- [x] CHK010 - Is the photo-consent rule (FR-009, `photos_internal`) applied identically across the daily feed (User Story 2) and the gallery (User Story 3)? [Consistency, Spec §FR-009, §FR-010] — confirmed consistent: FR-010 explicitly cross-references FR-009's rule rather than restating a separate one.

## Acceptance Criteria Quality

- [x] CHK011 - Can SC-003 ("100%... zero photos ever shown without consent") be objectively verified given the spec's own admission that per-photo child identification isn't technically possible? [Measurability, Spec §SC-003, §Assumptions] — reconciled: SC-003 is about the *viewing parent's own* consent gate (Assumptions' "Photo consent scope" note), which is fully deterministic and testable (a boolean flag check) — it is not claiming to solve the separately-acknowledged, admittedly-unenforceable "which children appear in the photo" problem. The two are independent claims; SC-003 only measures the enforceable one.
- [x] CHK012 - Is SC-004 ("no propagation delay beyond normal sync/cache timing") measurable without a defined cache TTL? [Measurability, Spec §SC-004] — assessed: acceptable as-is, consistent with how other shipped features' success criteria reference "normal sync timing" as a relative bound rather than inventing a new absolute number this feature doesn't otherwise need.

## Scenario Coverage

- [x] CHK013 - Are requirements defined for a caregiver attempting to record an activity when the tablet has no caregiver checked in yet (empty `recorded_by`)? [Coverage, Spec §Edge Cases] — covered: explicit edge case, "never blocking creation."
- [x] CHK014 - Are requirements defined for the exception/error flow when a photo upload fails after multiple retries? [Coverage, Exception Flow, Spec §Edge Cases] — covered: "the activity itself... is still visible; the failed photo shows a retry-needed state."
- [x] CHK015 - Are requirements defined for the recovery flow when an activity's metadata syncs successfully but one of its queued photos never does (partial sync)? [Coverage, Recovery, Spec §FR-012] — resolved: this is the same case as CHK014 generalized to the multi-photo case; FR-012 already establishes photos queue and sync independently of the parent activity's own metadata, so a partial-photo-sync state is an expected (not exceptional) intermediate state, not a distinct failure requiring new requirements text.
- [x] CHK016 - Are requirements defined for what a director sees/can do if they attempt to delete an activity that a caregiver is concurrently still uploading photos for (race)? [Coverage, Gap] — resolved: added edge case to spec.md — deleting an in-progress (still-uploading) activity is allowed like any other; a photo that finishes uploading after its parent activity was deleted is rejected server-side (`404`, the activity no longer exists) rather than silently reappearing, consistent with FR-011's "removed from every surface" guarantee.

## Edge Case Coverage

- [x] CHK017 - Are requirements defined for the maximum description length, and is it consistent with similar free-text fields elsewhere in the product? [Edge Case, Spec §Assumptions] — resolved: data-model.md sets 2000 chars, explicitly matched to feature 012a's `Notes` precedent (cited in data-model.md).
- [x] CHK018 - Are requirements defined for a child who has zero active contract (e.g., between contracts) viewing/being represented in group activities? [Edge Case, Gap] — resolved: FR-009's consent rule already defaults to "no active contract → photos withheld" (spec.md FR-009 explicit wording), and User Story 2's Acceptance Scenario 3 covers this exact case ("or no active contract").
- [x] CHK019 - Are requirements defined for a parent with children in the same group under two different contracts (e.g., twins) — does gallery/feed access de-duplicate the activity? [Edge Case, Gap] — resolved: added to spec.md Edge Cases — an activity is deduplicated per parent (shown once even if it applies to multiple of the parent's children in the same group), since the alternative (showing the same activity twice) would read as a bug, not a feature.

## Non-Functional Requirements

- [x] CHK020 - Are requirements defined for maximum acceptable photo-resize latency on the caregiver tablet (blocking vs. background)? [Gap, Spec §Performance] — resolved: plan.md's Technical Context states the target ("perceived as instant... on a synchronous 1-2 photo upload") — this is a soft UX target, not a hard SLA, consistent with how other tablet-latency requirements in this codebase (e.g., sync engine) are expressed.
- [x] CHK021 - Are accessibility requirements specified for the activity-type picker (icon-only vs. icon+label) consistently with design-system.md's "never convey semantic state by color alone" rule? [Consistency, Spec §UX Requirements] — confirmed: caregiver-tablet UX Requirements explicitly requires "icon+text pairing on the type picker (never color alone)."
- [x] CHK022 - Are security requirements explicit about what happens if a device token attempts to create an activity for a `groupId` that doesn't match its own claims? [Gap, Security] — resolved: this already follows the established device-claims pattern (FR-001: "groupId/locationId come from the device token's own claims, never client-supplied" per contracts/group-activities-api.md) — there is no client-suppliable `groupId` for a mismatch to occur against, so the question doesn't apply as a distinct requirement; confirmed consistent with `RecordChildEventCommand`'s identical precedent.

## Dependencies & Assumptions

- [x] CHK023 - Is the dependency on feature 008a's `RoomShift`/`IShiftAttributionService` for `recorded_by` resolution explicitly documented rather than assumed? [Traceability, Spec §Assumptions] — confirmed: spec.md's Assumptions section states this explicitly, with reasoning.
- [x] CHK024 - Is the assumption that no multi-photo/resize storage capability exists yet (net-new backend work) validated against the actual current codebase rather than guessed? [Assumption, Spec §Assumptions] — confirmed: validated via direct codebase research (documented in research.md R2/R3), not an unverified guess.
- [x] CHK025 - Is the assumption that no group-timeline UI exists in either caregiver or director web validated, not guessed? [Assumption, Spec §Assumptions] — confirmed: validated via direct codebase research (zero hits for group-scoped timeline UI in either app).

## Ambiguities & Conflicts

- [x] CHK026 - Is there a requirement & acceptance-criteria ID scheme established for traceability? [Traceability] — confirmed: `FR-###`/`SC-###` scheme consistent with every prior feature's spec.md.

## Notes

All 26 items resolved during this pass — three genuine gaps found and fixed directly in spec.md
(CHK007's timezone anchor, CHK016's concurrent-delete-during-upload edge case, CHK019's
same-group-twins deduplication edge case); the rest were confirmed already covered or correctly
scoped, with reasoning recorded per item above rather than left as unexplained checkmarks.
