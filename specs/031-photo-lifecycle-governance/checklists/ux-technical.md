# UX & Technical-Correctness Requirements Checklist: Photo Lifecycle & Governance

**Purpose**: Validate that spec.md's requirements around RBAC consistency, GDPR purge safety,
storage lifecycle correctness, and the parent/director UX are complete, unambiguous, and
measurable enough to implement without guessing — not a test of the implementation itself.
**Created**: 2026-07-19
**Feature**: [spec.md](../spec.md)

## RBAC Consistency

- [x] CHK001 Is the exact set of actions covered by "identical authorization" (upload, edit, delete, download) enumerated per photo type, rather than left as a general statement? [Clarity, Spec §FR-011]
- [x] CHK002 Are location-assignment semantics ("their location(s)") defined precisely enough to implement — e.g., does a staff member with multiple location assignments get access to photos at all of them or only a primary one? [Clarity, Spec §FR-001] — resolved via a new Assumptions bullet.
- [x] CHK003 Is the authorization requirement consistent between the User Story 4 acceptance scenarios and FR-011's wording, with no photo type left as an implicit exception? [Consistency, Spec §US4, §FR-011]
- [x] CHK004 Does the spec define what happens to authorization when a staff member's location assignment changes between an object's upload and a later action on it? [Edge Case, Spec Edge Cases] — resolved via a new Edge Cases bullet.
- [x] CHK005 Is there a requirement covering authorization for the caregiver-tablet (device-token) upload channel specifically, given it authenticates differently (no user role claim) than the staff/director JWT paths the other FRs describe? [Gap]

## GDPR Purge Safety

- [x] CHK006 Is "sole depicted child" (the condition that makes a group photo eligible for purge) defined with the same precision as the archive-eligibility derivation, rather than left to be inferred as identical? [Consistency, Spec §FR-010, §FR-003]
- [x] CHK007 Are the success criteria for purge (SC-002: "never deletes a photo that still depicts another active or not-targeted child") measurable by a concrete test scenario, or only asserted qualitatively? [Measurability, Spec §SC-002]
- [x] CHK008 Does the spec define the required behavior if the purge action is invoked twice in quick succession for the same child (idempotency / concurrent-request handling)? [Gap, Exception Flow] — resolved via a new Edge Cases bullet + FR-016 amendment.
- [ ] CHK009 Is the audit-log requirement (FR-017) specific about what "recorded" means operationally (queryable store vs. structured log line), or does it leave persistence mechanism fully open? [Clarity, Spec §FR-017] — intentionally deferred to plan.md (research.md R4 resolves it: structured `ILogger` entry, no new table); left open in spec.md by design since it's an implementation mechanism, not a product requirement.
- [x] CHK010 Are partial-failure requirements (FR-016) specific about what state the child/photo records are left in after a partial failure, and whether a retry is expected to be safe (won't re-attempt already-deleted objects incorrectly)? [Completeness, Spec §FR-016] — resolved via FR-016 amendment.
- [x] CHK011 Does the spec define who besides a Director may trigger the purge action, given FR-008 says "director or staff member" while the UX Requirements section frames it as a director-only flow? [Conflict, Spec §FR-008, §UX Requirements] — spec.md was correct throughout (Director/Staff); the conflict was in this feature's own contracts.md/tasks.md (wrongly scoped the endpoint to `DirectorOnly`), corrected during `/speckit-analyze`.

## Storage Lifecycle Correctness

- [x] CHK012 Is the general no-recent-activity tiering window (FR-005) defined with a concrete default value in the spec itself, or only in a separate Assumptions section? [Clarity, Spec §FR-005, §Assumptions]
- [x] CHK013 Does the spec distinguish "access" (used to justify the 90-day general tiering rationale) from "object creation age" (what the mechanism actually measures), or could a reader assume these are the same signal? [Ambiguity, Spec §FR-005] — resolved via a new Assumptions bullet.
- [x] CHK014 Are the storage-class transition requirements (FR-009/FR-010) explicit about transition direction guarantees — e.g., is downgrading a Coldline object back to a warmer tier ever required, or explicitly never required? [Completeness, Spec §FR-009, §FR-010] — resolved via a new Edge Cases bullet.
- [x] CHK015 Is the grace-period requirement (FR-002) clear on what "deactivated for longer than a configured grace period" means when a child is deactivated, reactivated, and deactivated again — does the clock reset? [Edge Case, Spec §FR-002, Edge Cases]
- [x] CHK016 Is FR-006 ("no functional or UI difference is visible") measurable/verifiable, or does it rely on a qualitative judgment of "visible"? [Measurability, Spec §FR-006] — SC-005 gives it a concrete, testable form (no reported functional regression).
- [x] CHK017 Does the spec define an expected upper bound on how long after the grace period elapses a transition must actually occur (job cadence expectation), or is "eventually, via a scheduled job" sufficient per the spec's own stated priority (P2, background)? [Gap, Spec §US3] — resolved via a new Assumptions bullet (daily cadence).

## UX Requirements Quality

- [x] CHK018 Is the parent-facing "download original" success criterion (SC-003: "two taps or fewer") specific about the starting screen it's measured from, so it's falsifiable rather than open to interpretation? [Measurability, Spec §SC-003] — resolved via SC-003 amendment.
- [x] CHK019 Are loading/empty/error states defined for the download action across all three photo types, or only illustrated for the general case? [Completeness, Spec §UX Requirements] — the requirement is intentionally type-agnostic (download behaves identically for all three from the parent's perspective); no per-type divergence exists to document.
- [x] CHK020 Are accessibility requirements (touch target size) specified for the purge action's confirmation dialog controls specifically, not just the entry-point action? [Coverage, Spec §UX Requirements] — resolved via the Accessibility line amendment.
- [x] CHK021 Is the offline behavior requirement for the download action ("hidden when offline") consistent with how other read-only actions in this app behave offline, or does the spec need to state why this one differs (no offline queue) explicitly enough to prevent a reviewer flagging it as an inconsistency? [Consistency, Spec §UX Requirements]
- [x] CHK022 Does the spec define the copy tone/wording expectation for the purge confirmation dialog (a destructive, compliance-sensitive action) consistently with this product's "warm, human, non-technical" parent-facing language principle, given the audience here is staff/director rather than parents? [Gap] — resolved via the i18n line amendment.

## Dependencies & Assumptions

- [x] CHK023 Is the assumption that `StaffOrDirector`/`ParentOnly` are sufficient for every access rule in this feature validated against every FR, or only asserted once generally in the Assumptions section? [Traceability, Spec §Assumptions]
- [x] CHK024 Is the dependency on existing consent gating (`Contract.Consent.PhotosInternal`) for group-photo parent access made explicit as a functional requirement, or only mentioned as an assumption? [Gap, Spec §Assumptions]

## Notes

- All findings from this checklist and the subsequent `/speckit-analyze` pass were fixed in
  spec.md, contracts/photo-lifecycle-api.md, plan.md, tasks.md, and quickstart.md before
  implementation — see each file's diff for the corresponding amendment. Only CHK009 remains
  open by design (a deliberate implementation-mechanism deferral to plan.md, not a product gap).
- The most consequential finding (CHK011 / analyze F1) was a real inconsistency this feature's
  own plan artifacts introduced against an already-correct spec: the purge endpoint's
  authorization was drafted as `DirectorOnly` in contracts.md/tasks.md, contradicting spec.md's
  own FR-008 ("director or staff member"). Corrected to `StaffOrDirector` throughout.
