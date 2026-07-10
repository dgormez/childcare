# Requirements Quality Checklist: Caregiver Scheduling (Weekly Staff Rota)

**Purpose**: Validate spec.md's functional requirements for clarity, testability, and
consistency before implementation — "unit tests for the requirements," not a test of the
(not-yet-built) implementation.
**Created**: 2026-07-10
**Feature**: [spec.md](../spec.md)

## Requirement Completeness

- [x] CHK001 Is authorization explicitly required for every read endpoint, not just writes? [Completeness] — gap found: FR-014 covered writes only; added **FR-015** covering all read endpoints except FR-012's own-schedule read.
- [x] CHK002 Is a rota-copy request's target-week validity (must be after the source week, must not target an already-passed week) specified as a requirement? [Completeness, Gap] — gap found: only implied in contracts.md, not stated as an FR; added **FR-016** and a corresponding US2 acceptance scenario.
- [x] CHK003 Is the overlap-rejection rule (FR-003) complete for same-location double-booking, not just cross-location? [Completeness] — gap found: original wording only covered "two different locations"; broadened FR-003 to cover any overlap regardless of location, and propagated the fix to SC-002, data-model.md, and tasks.md.
- [x] CHK004 Are requirements present for mid-week staff additions (FR-011), unassigned-group entries (edge case), and deactivated-staff handling (FR-009b)? [Completeness, Spec §Edge Cases]
- [x] CHK005 Is a "week" definition (which day it starts on) stated anywhere in the spec? [Completeness, Gap] — gap found: added to Assumptions (Monday–Sunday, ISO-8601).

## Requirement Clarity

- [x] CHK006 Is "projected on-duty count" clearly and consistently distinguished from feature 010's live BKR ratio everywhere it's mentioned, with no stale references implying they're the same computation? [Clarity, Spec §FR-006/FR-007] — three stale references (User Story 3 intro, User Impact, Cross-platform Impact) found during this pass, describing the pre-correction "feeds live BKR" behavior; all rewritten to match FR-007's corrected description.
- [x] CHK007 Is "future-dated" / "past-dated" unambiguous, i.e. is the reference clock specified? [Clarity, Spec §FR-004] — yes, Assumptions ties all date handling to `Europe/Brussels` local time, matching the existing `BelgianCalendarDay` convention referenced in data-model.md.
- [x] CHK008 Is the absence-reason set (sick/leave/holiday) closed and unambiguous, with required-iff-absent stated? [Clarity, Spec §FR-005] — yes, data-model.md's validation rules state this explicitly.
- [x] CHK009 Are the two rota-copy skip conditions (closure day vs. existing entry) distinguishable in the response contract, not merged into one generic "skipped" reason? [Clarity, Spec §FR-009/FR-009a] — yes, contracts.md's `reason: closure_day | existing_entry` makes this explicit.

## Requirement Consistency

- [x] CHK010 Do the Product Context's Data Flow/Outputs/Cross-platform Impact sections agree with the corrected FR-006/FR-007 (projected count vs. live BKR), or do any still describe the pre-correction behavior? [Consistency] — found and fixed three inconsistent sentences (see CHK006).
- [x] CHK011 Does User Story 1/2's "Why this priority"/"Independent Test" prose reference story dependencies (e.g., "BKR integration") using terminology that still matches the corrected scope? [Consistency] — found loose "BKR integration" shorthand in US1/US2 prose that could mislead after the FR-007 correction; reworded to name the actual dependent story (own-schedule read) instead.
- [x] CHK012 Are DirectorOnly/StaffOrDirector authorization boundaries stated consistently between spec.md's Technical Requirements, FR-014/FR-015, and contracts/staff-schedules-api.md? [Consistency] — yes, after CHK001's fix all three now agree.

## Acceptance Criteria Quality

- [x] CHK013 Are SC-001 through SC-005 measurable/verifiable without implementation knowledge? [Measurability, Spec §Success Criteria] — yes; SC-002/SC-003/SC-004/SC-005 are 100%-threshold pass/fail checks, SC-001 is a time-boxed usability target consistent with prior features' (e.g. 011's) SC style.
- [x] CHK014 Does SC-003 correctly scope its claim to the projected on-duty count rather than overclaiming an effect on feature 010's live ratio? [Measurability, Spec §SC-003] — yes, explicitly scoped after the FR-007 correction.

## Scenario Coverage

- [x] CHK015 Are Primary (build), Alternate (copy), and a Recovery/correction flow (edit before date passes) all covered? [Coverage] — yes, US1 scenarios 4-5.
- [x] CHK016 Is an Exception/rejection flow covered for each write operation (overlap, past-date, invalid copy target, missing absence reason)? [Coverage] — yes, across FR-003/FR-004/FR-005/FR-016 and their corresponding acceptance scenarios/contract errors.
- [x] CHK017 Is the zero-state (empty week, staff with no shifts) covered for both the director and caregiver-read surfaces? [Coverage, Edge Case] — yes, spec.md's UX Requirements (empty-state sentence) and US4 scenario 2 (empty own-schedule read).

## Edge Case Coverage

- [x] CHK018 Concurrent-write overlap race — addressed? [Edge Case, Spec §Edge Cases] — yes, explicit edge case + FR-003 + research.md R2 (advisory lock).
- [x] CHK019 Deactivated staff with future-dated entries — addressed? [Edge Case] — yes, FR-009b + dedicated Clarifications entry.
- [x] CHK020 Unassigned group (floater) — addressed, and confirmed not to block eligibility? [Edge Case] — yes.
- [x] CHK021 Scheduled-but-never-checked-in staff member — addressed, with the live-BKR interaction made explicit? [Edge Case] — yes, added as its own edge case bullet during the R1 correction.

## Non-Functional Requirements

- [x] CHK022 Is an i18n requirement stated for all new user-facing UI strings? [Non-Functional, Spec §FR-013] — yes.
- [x] CHK023 Are performance expectations for the rota-copy bulk operation and projected-on-duty lookups stated? [Non-Functional, Spec §Technical Requirements] — yes (indexing requirements named explicitly).
- [x] CHK024 Are accessibility requirements (keyboard reachability, focus ring) stated for the director-web grid? [Non-Functional, Spec §UX Requirements] — yes.

## Dependencies & Assumptions

- [x] CHK025 Is the assumption that feature 010's BKR computation is unaffected validated against the actual shipped code, not just asserted? [Assumption] — yes, verified directly against `GetBkrRatioQuery.cs` during planning (research.md R1), not assumed from the BACKLOG prompt.
- [x] CHK026 Is the caregiver-facing-UI-deferred-to-027 scope decision traceable to an explicit decision rather than an unstated default? [Assumption, Traceability] — yes, resolved via `AskUserQuestion` before specification began; documented in Assumptions.
- [x] CHK027 Is the dependency on feature 011's `KdvClosureDay` for rota-copy exclusion named explicitly? [Dependency] — yes, FR-009 references feature 011 by number.

## Notes

- This pass found and fixed four genuine gaps/inconsistencies (CHK001, CHK002, CHK003, CHK005)
  and three stale cross-references left over from the mid-planning BKR-integration correction
  (CHK006/CHK010/CHK011) — all fixed directly in spec.md/data-model.md/contracts/tasks.md, not
  logged as deferred debt, per the standing process rule.
- No items remain unresolved.
