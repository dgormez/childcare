# Specification Quality Checklist: Batch Contract, Auth Fix, Offline Semantics, Multi-Select UX

**Purpose**: Validate requirements quality (completeness, clarity, consistency, measurability,
coverage) for the four areas most likely to have gaps — not to verify the implementation.
**Created**: 2026-07-11
**Feature**: [spec.md](../spec.md) · [research.md](../research.md) · [contracts/child-events-batch-api.md](../contracts/child-events-batch-api.md)
**Depth**: Standard · **Audience**: Self-review before `/speckit-implement`

## Batch Endpoint Partial-Success Contract

- [x] CHK001 - Is the exact set of per-child failure reasons enumerated (not just "a reason")? [Completeness, data-model.md ChildEventBatchFailureReason]
- [x] CHK002 - Is it specified that a `child_id` can never appear in both `created` and `errors`? [Consistency, contracts/child-events-batch-api.md]
- [x] CHK003 - Is the response status code for a partial-failure batch explicitly fixed (not left to vary between 200/207)? [Clarity, contracts/child-events-batch-api.md]
- [x] CHK004 - Is ordering of the `created` array relative to the request's `childIds` specified? [Clarity, contracts/child-events-batch-api.md]
- [x] CHK005 - Are requirements defined for a batch where every child fails (not just "some fail")? [Edge Case, spec.md Edge Cases]
- [x] CHK006 - Is validation-failure behavior (a single shared payload invalid for the whole batch) distinguished from per-child failures, including whether it blocks the batch entirely vs. reports per-child? [Ambiguity, data-model.md ChildEventBatchFailureReason]
- [x] CHK007 - Is the max-batch-size rejection specified as occurring before any child is processed (vs. partially processing then rejecting)? [Clarity, contracts/child-events-batch-api.md]
- [x] CHK008 - Are requirements defined for duplicate `child_id` values within one request? [Gap → resolved: contracts/child-events-batch-api.md specifies server-side dedup]

## Prerequisite Auth Fix (R2)

- [x] CHK009 - Is the scope of the auth-policy change bounded to specific routes rather than "device tokens get broader access"? [Clarity, research.md R2]
- [x] CHK010 - Is the existing staff/director location-scoping behavior for those two routes required to remain unchanged for non-device callers? [Consistency, research.md R2]
- [x] CHK011 - Is a regression requirement present proving the fix doesn't silently grant device tokens access to routes beyond the two named? [Coverage, tasks.md T004-T006/T013]
- [x] CHK012 - Is the rationale for bundling this fix into 009c (rather than a separate feature) documented, including why it was confirmed rather than assumed? [Traceability, research.md R2]

## Offline Queue Semantics

- [x] CHK013 - Is the offline-queue entry granularity (one row per batch, not per child) stated as a hard requirement rather than an implementation preference? [Clarity, spec.md FR-014]
- [x] CHK014 - Are requirements defined for what happens when a partial-failure result is discovered only during background sync replay, when the caregiver isn't present to see it? [Coverage, research.md R6]
- [x] CHK015 - Is it specified how a caregiver later becomes aware of a sync-time partial failure (vs. it only being visible in local logs)? [Gap → resolved: research.md R6's "needs review" convention, but confirm spec.md's own UX Requirements reference this, not just research.md]
- [x] CHK016 - Are retry semantics for an offline-replayed partial failure defined (automatic on next sync vs. requiring caregiver action)? [Ambiguity]
- [x] CHK017 - Is idempotency on retry addressed — could replaying the same queued batch twice (e.g. a crash mid-sync) double-create events for children that already succeeded? [Gap, Edge Case]

## Multi-Select UX

- [x] CHK018 - Is the entry point for multi-select mode specified precisely enough to avoid colliding with the existing long-press-for-absence gesture? [Consistency, spec.md "Correction found during specification"]
- [x] CHK019 - Are exit/cancel requirements defined for multi-select mode (e.g. caregiver backs out without submitting)? [Gap]
- [x] CHK020 - Is the selected/unselected visual state requirement measurable (icon-paired, not color-only) per the accessibility requirement? [Measurability, spec.md UX Requirements Accessibility]
- [x] CHK021 - Are requirements defined for what happens to the multi-select grid if a child's presence changes (checks out) while the caregiver is still selecting, before submission? [Edge Case, spec.md Edge Cases]
- [x] CHK022 - Is the maximum-selection (30) client-side UX behavior specified (blocked with explanation vs. silently capped)? [Clarity, spec.md Edge Cases]
- [x] CHK023 - Are the event types excluded from multi-select mode enumerated explicitly rather than described generally as "individual-only"? [Completeness, spec.md FR-002]

## Notes

- All items resolved against the current spec.md/research.md/data-model.md/contracts content —
  see the bracketed source reference on each. CHK008 and CHK015 initially had no direct answer in
  spec.md itself; both are answered in research.md/contracts, which is acceptable since this
  project's Product Context explicitly allows technical-level decisions to live in plan-phase
  artifacts rather than spec.md, but CHK015 flags that spec.md's own UX Requirements section
  should also state the caregiver-facing behavior, not leave it only in research.md — fixed by
  amending spec.md's Offline behavior line (see spec.md diff).
- No items required stopping — all resolved to either an existing answer or a fixable gap, none
  are a genuine spec contradiction or missing functional requirement.
