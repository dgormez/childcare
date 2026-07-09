---

description: "Requirements quality checklist for feature 010-attendance"
---

# Specification Quality Checklist: Daily Attendance Registration

**Purpose**: Validate requirements quality (completeness, clarity, consistency, measurability,
coverage) — not implementation behavior.
**Created**: 2026-07-09
**Feature**: [spec.md](../spec.md)
**Depth**: Standard | **Audience**: Reviewer (PR) | **Focus**: BKR/regulatory-compliance
requirement clarity; offline-sync/conflict-consistency requirement completeness (top two
relevance clusters given this feature's regulatory and offline-first nature — selected
autonomously per the standing single-pass process, no interactive session available)

## Requirement Completeness

- [x] CHK001 - Are requirements defined for what happens when a caregiver attempts to check in a
  child who is already marked absent for the day (status transition from absent → present)?
  [Gap, Spec §FR-001/FR-005] — Resolved: FR-001a.
- [x] CHK002 - Is a requirement defined for what happens when `check-out` is called on a record
  that is not currently `present` (e.g., already checked out, or absent)? [Gap, Spec §FR-002] —
  Resolved: FR-002a.
- [x] CHK003 - Are requirements defined for concurrent absence-mark and check-in requests racing
  for the same child/location/day (which one wins, and is the loser a 409 or silently ignored)?
  [Gap, Spec Edge Cases] — Resolved: FR-005's race clause.
- [x] CHK004 - Is there a requirement covering what a caregiver or director sees/receives when a
  BKR read (FR-007) is requested for a location with zero `RoomShift` data at all (never set up)
  versus zero currently checked-in staff? [Completeness, Spec §FR-007b] — Resolved: FR-007b now
  states both cases are indistinguishable and handled identically.

## Requirement Clarity

- [x] CHK005 - Is the BKR "amber" threshold boundary (as distinct from green/red) quantified with
  a specific numeric rule, or left to UI-layer judgment? [Clarity, Spec §Key Entities/BKR Ratio] —
  Resolved: FR-007e.
- [x] CHK006 - Is "at least half" in the nap-time inference rule (FR-007c) precise about rounding
  behavior for an odd present-count (e.g., 3 present children, is 1 or 2 the threshold)?
  [Ambiguity, Spec §FR-007c] — Resolved: FR-007c now states the exact `nappingCount × 2 ≥
  presentCount` formula.
- [x] CHK007 - Is "a couple of taps" in the absence-marking success criterion quantified with an
  exact maximum tap count, matching the precision FR-021-style requirements use elsewhere in this
  codebase? [Clarity, Spec §UX Requirements] — Resolved: FR-017 now specifies a 3-tap maximum.

## Requirement Consistency

- [x] CHK008 - Do FR-010/FR-011 (caregiver same-day/device-location correction) and FR-011
  director any-day correction use terminology consistent with feature 009's equivalent
  requirements (e.g., "same calendar day" anchored to the same `Europe/Brussels` boundary),
  without needing to re-derive the boundary rule from scratch? [Consistency, Spec §FR-010/FR-011]
  — Resolved: FR-010 now names the `Europe/Brussels` boundary explicitly.
- [x] CHK009 - Is the relationship between `Status = absent` and the BKR present-count exclusion
  (User Story 3, Acceptance Scenario 3) stated as a functional requirement, not only as an
  acceptance scenario, so it's independently traceable? [Traceability, Spec §FR-007] — Resolved:
  FR-007d.

## Acceptance Criteria Quality

- [x] CHK010 - Can SC-003 ("100% of tested boundary combinations") be objectively verified without
  first enumerating what the full set of "boundary combinations" is (solo/2+, nap/non-nap,
  zero-staff)? [Measurability, Spec §SC-003] — Resolved: SC-003 now enumerates the exact
  present-count values tested at each threshold.
- [x] CHK011 - Is SC-005's "non-fabricated" `planned_duration_minutes` value measurable against a
  specific test oracle (the contract's own `ContractedDay` entry), or is it only qualitatively
  described? [Measurability, Spec §SC-005] — Resolved: SC-005 now names `Contract.ContractedDays`
  as the explicit oracle.

## Scenario Coverage

- [x] CHK012 - Are recovery/rollback requirements defined for a director correction that itself
  fails partway (e.g., a status change to `present` without a corresponding `checkInAt`)? [Gap,
  Exception Flow] — Resolved: FR-011a.
- [x] CHK013 - Is a requirement defined for a child with two contracts at two different locations
  in the same tenant (feature 007's split-location case) being checked in at both locations on
  the same day — does `planned_duration_minutes` derivation correctly select the contract
  matching the specific `LocationId` on the record, not any of the child's contracts tenant-wide?
  [Coverage, Spec §Assumptions] — Resolved: promoted from Assumptions-only to FR-006's own text.
- [x] CHK014 - Are non-functional requirements (BKR read latency/polling frequency from the
  caregiver tablet) specified, or left entirely to the Technical Requirements' qualitative "cheap
  enough" language? [Gap, Spec §Technical Requirements] — Resolved: FR-008a/SC-006.

## Dependencies & Assumptions

- [x] CHK015 - Is the assumption that `RoomShift` roster data is a reliable proxy for "on-duty
  qualified staff" (as opposed to a scheduling-based source, not yet built per feature 012)
  explicitly validated against what happens if a caregiver forgets to check in via 008a's PIN
  flow while physically present? [Assumption, Spec §Assumptions] — Resolved: Assumptions now
  states this as an accepted, explicitly-reasoned limitation.
- [x] CHK016 - Is the leefgroep BKR carve-out's scope boundary (which locations/groups are
  affected) clear enough that a reviewer can confirm no location silently falls through a gap
  between "ratio-based" and "leefgroep-based" enforcement? [Clarity, Spec §Assumptions] —
  Resolved: Assumptions now states every location is covered by the ratio-based regime uniformly.
