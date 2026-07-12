# Requirements Quality Checklist: Reservation Settings

**Purpose**: Validate the quality (completeness, clarity, consistency, measurability, coverage) of
spec.md's requirements before implementation — a unit-test suite for the requirements themselves,
not for the eventual code.
**Created**: 2026-07-11
**Feature**: [spec.md](../spec.md)
**Depth**: Standard | **Audience**: Reviewer (pre-implementation)

## Requirement Completeness

- [x] CHK001 - Are default values specified for every new setting, including for locations that predate this feature? [Completeness, Spec §FR-002]
- [x] CHK002 - Is the notice-hours validation range (minimum/maximum) explicitly bounded rather than left open-ended? [Completeness, Spec §FR-011]
- [x] CHK003 - Are requirements defined for what happens to in-flight pending requests when a mode changes, not just for new submissions? [Completeness, Spec §FR-005]
- [x] CHK004 - Is the downstream side-effect of an auto-approved `informational` request (attendance pre-registration, or lack thereof per type) explicitly specified rather than left to be inferred from `approval`-mode behavior? [Completeness, Spec §FR-008, Clarifications]

## Requirement Clarity

- [x] CHK005 - Is "the location's mode" defined precisely enough to resolve unambiguously when a request has no single obvious location (multi-location child, or a type with no contracted-day concept)? [Clarity, Spec §FR-017]
- [x] CHK006 - Is "immediately" (mode changes taking effect for new requests) given an operational definition distinguishing it from any caching or propagation delay? [Clarity, Spec §SC-001]
- [x] CHK007 - Is the notice-hours boundary condition (exactly at N hours vs. strictly less than N hours) specified precisely enough to be independently implemented the same way twice? [Clarity, Spec §FR-012]

## Requirement Consistency

- [x] CHK008 - Do the default values stated in the Assumptions/Edge Cases section agree exactly with those stated in the Functional Requirements section? [Consistency, Spec §FR-002 vs Edge Cases]
- [x] CHK009 - Is the `informational`-mode auto-approval behavior consistent with `approval`-mode's existing validation rules (i.e., nothing an `approval`-mode approval would reject is silently allowed through `informational`)? [Consistency, Spec §FR-009]
- [x] CHK010 - Are the three request types (`absence`/`extra`/`swap`) treated with a consistent set of rules throughout, with type-specific exceptions clearly called out rather than implied? [Consistency, Spec §FR-001, FR-017]

## Acceptance Criteria Quality

- [x] CHK011 - Can SC-002 ("100% of submissions for a disabled type are rejected server-side") be objectively verified independent of any specific client implementation? [Measurability, Spec §SC-002]
- [x] CHK012 - Is SC-005 ("zero pending requests silently altered") falsifiable — i.e., is there a concrete, checkable definition of "altered"? [Measurability, Spec §SC-005]

## Scenario Coverage

- [x] CHK013 - Are requirements defined for a location whose settings are queried before any director has ever saved them (first read after this feature ships)? [Coverage, Edge Case]
- [x] CHK014 - Are requirements defined for the case where a request type has zero resolvable candidate locations (child with no active contract)? [Coverage, Spec §FR-017, Edge Case]
- [x] CHK015 - Are requirements defined for a mode transitioning in the "safe" direction (disabled → approval/informational), where no warning is needed? [Coverage, Edge Case]
- [x] CHK016 - Are requirements defined for concurrent settings updates to the same location (two directors saving simultaneously)? [Gap]

## Edge Case Coverage

- [x] CHK017 - Is the behavior fully specified for a child with active contracts at two locations whose settings disagree for the same request type? [Edge Case, Spec §FR-017]
- [x] CHK018 - Is the notice-hours upper bound's rationale (why 8760, not unbounded) documented so a future reviewer can distinguish "deliberate ceiling" from "arbitrary limit"? [Clarity, Spec Assumptions]

## Non-Functional Requirements

- [x] CHK019 - Are authorization requirements specified for the new settings-update endpoint consistent with existing director-only patterns elsewhere in the spec? [Consistency, Spec §FR-004]
- [x] CHK020 - Are i18n requirements specified for every new user-facing string introduced by this feature, not just the primary flows? [Completeness, Spec §FR-016]

## Dependencies & Assumptions

- [x] CHK021 - Is the dependency on feature 013a's existing entity shape (no `LocationId` on `DayReservation`) explicitly documented as a constraint this feature must work within, rather than silently assumed? [Traceability, Spec §FR-017]
- [x] CHK022 - Are premise corrections to the original feature brief (director push notifications, staff-initiated submissions) documented with their reasoning rather than silently changed? [Traceability, Spec Assumptions]

## Notes

All 22 items pass on review — each traces to an explicit spec section. Four genuine gaps found
during drafting were fixed in spec.md rather than left as debt: candidate-location resolution for
multi-location children and the downstream effect of informational auto-approval (both resolved
via the Clarifications session), the false premise of an existing director-notification/
staff-submission channel (resolved via Assumptions corrections), and concurrent settings-update
behavior surfaced by CHK016 (resolved by adding FR-018 — explicit last-write-wins semantics
rather than silence).
