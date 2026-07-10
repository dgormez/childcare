# Specification Quality Checklist: Parent Communication — Requirements Quality

**Purpose**: Validate requirements completeness, clarity, and consistency before `/speckit-plan`/`/speckit-implement` (already past plan/tasks — this pass re-validates the spec against what planning surfaced).
**Created**: 2026-07-10
**Feature**: [spec.md](../spec.md)
**Depth**: Standard | **Audience**: Author (self-review before implementation) | **Focus**: Completeness & consistency across the account-provisioning, messaging, and notification requirement clusters, since these are the newest/highest-risk (first parent-facing surface in the codebase).

## Requirement Completeness

- [x] CHK001 - Is the eligibility gate for who a director can invite to the parent app explicitly and unambiguously defined? [Completeness, Spec §FR-000a]
- [x] CHK002 - Is the behavior when a parent account already exists for a contact (re-invite attempt) specified? [Completeness, Spec §Contracts — `errors.parent_invitation.already_has_account`]
- [x] CHK003 - Is participant visibility defined for a **general** (non-child-specific) thread, not just child-scoped ones? [Gap → Fixed, Spec §FR-004]
- [x] CHK004 - Is the reachability boundary for an announcement (which contacts actually receive it vs. which are merely "in scope") explicitly stated, not left implicit? [Gap → Fixed, Spec §FR-008]
- [x] CHK005 - Is post-departure/deactivation behavior for a parent's account access addressed, even if only to explicitly defer it? [Gap → Fixed, Spec §Assumptions]

## Requirement Clarity

- [x] CHK006 - Is "read state" for a `Message` defined precisely enough to match the two-party (family↔KDV) model rather than implying a per-individual read receipt that isn't actually built? [Clarity → Fixed, Spec §Key Entities]
- [x] CHK007 - Is the cross-reference in the Edge Cases section ("no director/staff participant yet... per FR routing rules") pointing at the requirement that actually answers it? [Clarity → Fixed, Spec §Edge Cases]
- [x] CHK008 - Is "one active push token per parent account" (not a multi-device fan-out) stated explicitly rather than left to be inferred from the data model? [Clarity, Spec §FR-012]

## Requirement Consistency

- [x] CHK009 - Does FR-008's announcement reach claim agree with FR-000a/FR-000b's account-gated access model (i.e., "reach" cannot exceed "has an account")? [Consistency → Fixed, Spec §FR-008]
- [x] CHK010 - Do the shared-family-thread requirements (FR-003a, FR-006a) and the Key Entities description of `Message Thread` describe the same participant model without contradiction? [Consistency, Spec §FR-003a/FR-006a/Key Entities]
- [x] CHK011 - Does the eligibility gate for parent-app invitation (`can_pickup = true` + email) stay consistent between User Story 0's acceptance scenarios, FR-000a, and the Assumptions section? [Consistency, Spec §User Story 0/FR-000a/Assumptions]

## Acceptance Criteria Quality

- [x] CHK012 - Are SC-003 and SC-007 (the two security-critical success criteria) phrased so they are objectively testable by an automated suite, not just observably true in a demo? [Measurability, Spec §SC-003/SC-007]
- [x] CHK013 - Is SC-000 (account provisioning) measurable independent of any other success criterion, so it can be validated before US1/US2 exist? [Measurability, Spec §SC-000]

## Scenario Coverage

- [x] CHK014 - Are Primary (happy path), Alternate (two-parent shared thread), Exception (unauthorized access, expired invitation), and Recovery (push failure → in-app fallback) scenario classes all represented? [Coverage, Spec §User Scenarios]
- [x] CHK015 - Is the concurrent-reply edge case (two staff replying near-simultaneously) addressed for ordering/no-data-loss, given messaging is a new write-heavy surface? [Coverage, Spec §Edge Cases]

## Edge Case Coverage

- [x] CHK016 - Is the zero-recipient announcement case (location/group with no enrolled children) explicitly a non-error outcome? [Edge Case, Spec §Edge Cases]
- [x] CHK017 - Is the "contact with no email/push token" case addressed for both the daily-summary/messaging access path (blocked, since no account) and the notification-delivery path (not blocked, in-app fallback)? [Edge Case, Spec §Edge Cases/Assumptions]

## Non-Functional Requirements

- [x] CHK018 - Is tenant isolation restated as an explicit requirement for this feature's new data, not just inherited silently from the constitution? [Coverage, Spec §FR-018]
- [x] CHK019 - Is internationalization scope (including push notification body locale) stated for every new user-facing surface this feature introduces? [Completeness, Spec §FR-016]

## Dependencies & Assumptions

- [x] CHK020 - Is the reuse of existing infrastructure (push sender, daily-summary query) documented as an assumption/dependency rather than silently assumed? [Traceability, Spec §Assumptions]
- [x] CHK021 - Is the out-of-scope status of day-reservation request notifications (feature 013a) explicit, given the original prompt named "request approved/rejected" as a trigger? [Completeness, Spec §Assumptions]

## Notes

- Five real gaps/inconsistencies were found on this pass (CHK003, CHK004, CHK005, CHK006, CHK007/CHK009) and fixed directly in `spec.md`, not logged as deferred debt — per this project's standing process rule. See spec.md's Clarifications section and the corresponding FR/Key Entity edits.
- All items pass after the fixes above.
