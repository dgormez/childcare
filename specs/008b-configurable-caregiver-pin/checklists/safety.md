# Specification Quality Checklist: Configurable Caregiver PIN — Safety & Regulatory Requirements Review

**Purpose**: Validate that the requirements themselves (not the implementation) adequately cover
safety, regulatory-compliance, and identity-assurance concerns before `/speckit-plan` artifacts
are implemented.
**Created**: 2026-07-13
**Feature**: [spec.md](../spec.md)
**Focus**: Safety/regulatory-compliance requirements quality (this feature's own risk: reducing
identity assurance at check-in); general requirements-quality (completeness, clarity,
consistency, measurability).
**Depth**: Standard **Audience**: Reviewer (pre-implementation) **Timing**: Post-clarify,
pre-plan-execution

## Requirement Completeness

- [x] CHK001 - Are requirements defined for what happens to a caregiver with no PIN ever set
      when the location's requirement is off? [Completeness, Spec Edge Cases]
- [x] CHK002 - Are requirements defined for the director-facing copy that must accompany the
      toggle, not just the toggle's existence? [Completeness, Spec §FR-003]
- [x] CHK003 - Is the default value for a location that has never touched this setting
      explicitly specified? [Completeness, Spec §FR-002]
- [x] CHK004 - Are requirements defined for what a director sees if they attempt to save the
      setting while offline or the save request fails (network/validation error)? [Completeness,
      Spec §FR-015]
- [x] CHK005 - Are requirements defined for whether the setting change is audited (who changed
      it, when) for later incident review, given this is an identity-assurance-reducing action?
      [Completeness, Spec §FR-016]

## Requirement Clarity

- [x] CHK006 - Is "immediately" (FR-004, FR-008 — check-in/out completes with no PIN step)
      quantified or otherwise unambiguous about timing expectations? [Clarity, Spec §FR-004]
- [x] CHK007 - Is the boundary between this feature's setting and the existing offline-skip-PIN
      behavior for administrator confirmation stated clearly enough to prevent the two being
      conflated during implementation? [Clarity, Spec Edge Cases]
- [x] CHK008 - Is "self-asserted identity" (used repeatedly as the tradeoff description) defined
      in terms a non-technical director will understand when reading the toggle's copy, or only
      in spec-internal language? [Clarity, Spec §FR-003 — rewritten to plain operational language]

## Requirement Consistency

- [x] CHK009 - Are the requirements for routine check-in/check-out (FR-004) and for the
      medical/sensitive-action confirmation step (FR-013) consistent in stating that only the
      former is affected by this feature's setting? [Consistency, Spec §FR-004, §FR-013]
- [x] CHK010 - Do the BKR-ratio requirement (FR-011) and the attribution requirement (FR-012)
      both anchor to the same underlying claim (identical shift record regardless of PIN path),
      avoiding two independently-stated but subtly different guarantees? [Consistency, Spec
      §FR-005, §FR-011, §FR-012]

## Acceptance Criteria Quality

- [x] CHK011 - Can SC-003's "100% of shift records ... produce identical staffing-ratio and
      event/incident-attribution results" be objectively verified without inspecting
      implementation internals? [Measurability, Spec §SC-003]
- [x] CHK012 - Can SC-004's "zero existing locations experience a behavior change" be verified
      independent of how the default is implemented (e.g., via observable location behavior, not
      by reading a column default)? [Measurability, Spec §SC-004]
- [x] CHK013 - Is there a measurable acceptance criterion for the director-facing tradeoff copy
      itself (e.g., that it names the specific risk), or does the requirement rely only on
      qualitative review? [Measurability, Spec §SC-005]

## Scenario Coverage

- [x] CHK014 - Is the "PIN requirement re-enabled mid-day while caregivers are checked in"
      scenario's expected behavior fully specified (which shifts are affected, which are not)?
      [Coverage, Spec Edge Cases]
- [x] CHK015 - Is the "wrong caregiver tapped, no PIN to catch it" scenario's correction path
      specified, even though no automated correction exists? [Coverage, Spec Edge Cases]
- [x] CHK016 - Is there a requirement covering a location's setting changing while an offline
      caregiver tablet still has stale cached roster data reflecting the old setting? [Coverage,
      Spec Edge Cases]

## Non-Functional Requirements (Safety/Regulatory Focus)

- [x] CHK017 - Does the spec require server-side (not client-trusted) enforcement of the
      PIN-skip decision, given this is the exact class of check a compromised/buggy client could
      otherwise bypass? [Completeness, Spec §FR-007]
- [x] CHK018 - Are the regulatory-compliance requirements (BKR ratio, attribution) explicitly
      called out as non-negotiable/unaffected, rather than left as an implicit assumption?
      [Clarity, Spec §FR-011, §FR-012]
- [x] CHK019 - Is there a requirement stating whether this setting itself (as opposed to its
      downstream effects) needs to be visible/reportable to an external auditor or inspector
      reviewing a KDV's compliance posture? [Completeness, Spec §FR-018]

## Dependencies & Assumptions

- [x] CHK020 - Is the assumption that "tap-to-identify remains mandatory in all cases" explicitly
      documented rather than merely implied by the feature's framing? [Assumption, Spec
      Assumptions]
- [x] CHK021 - Is the dependency on the existing PIN-lifecycle mechanism (set/reset/rate-limit)
      remaining unchanged explicitly stated as an assumption/out-of-scope boundary? [Assumption,
      Spec Assumptions, Out of Scope]

## Ambiguities & Conflicts

- [x] CHK022 - Is there any residual ambiguity about whether a location can have *some*
      caregivers PIN-exempt and others not (per-caregiver override), or is this unambiguously a
      whole-location, all-or-nothing setting? [Clarity, Spec §FR-017]

## Notes

- All items resolved (22/22 passing) — spec.md gained FR-015 (save-failure handling), FR-016
  (audit logging), FR-017 (whole-location scope), FR-018 (not a compliance-reporting artifact),
  SC-005 (measurable tradeoff-copy criterion), FR-003 rewritten in plain language, and two Edge
  Cases (offline-tablet stale setting, whole-location scope). tasks.md gained T008a/T008b/T015a/
  T015b to implement FR-015/FR-016.
