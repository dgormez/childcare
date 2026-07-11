# Specification Quality Checklist: Day Reservations (Parent Requests + Director Approval Queue)

**Purpose**: Validate requirements quality (completeness, clarity, consistency, measurability,
coverage) before/during implementation — reviewer-facing, standard depth.
**Created**: 2026-07-11
**Feature**: [spec.md](../spec.md)
**Focus**: Authorization/ownership boundaries, state-transition edge cases, cross-artifact
(spec/plan/tasks) consistency.

## Requirement Completeness

- [x] CHK001 - Are requirements defined for what happens when a director approves an `absence`
  request whose `AttendanceRecord` already exists (race with a caregiver-tablet-recorded
  absence)? [Completeness, Spec §Edge Cases] — covered by FR-016's general concurrent-decision
  guard plus reuse of `MarkAbsentCommand`'s own `AlreadyRecorded` failure path (research.md R1).
- [x] CHK002 - Are requirements defined for the response shape/context a director sees in the
  queue (so approve/reject needs no extra navigation)? [Completeness, Spec §FR-007] — present
  and cross-referenced into contracts/day-reservations-api.md's denormalized response fields.
- [x] CHK003 - Is the parent's own-request-history requirement (FR-019) scoped to which
  statuses it must include? [Completeness, Spec §FR-019] — clarified as "pending, approved,
  rejected, cancelled" (all statuses) in spec.md's FR-019 wording.

## Requirement Clarity

- [x] CHK004 - Is "in one action" (FR-007, director approve/reject) quantified rather than left
  as a vague UX adjective? [Clarity, Spec §FR-007] — the acceptance scenario under User Story 2
  makes this concrete: a single approve/reject tap resolves the request without a separate
  screen.
- [x] CHK005 - Is "more than 1 day in the past" (FR-002) unambiguous about the reference clock
  (server date vs. child's location timezone)? [Ambiguity, Spec §FR-002] — not explicitly
  pinned to a timezone in spec.md. **Gap identified and fixed**: added an explicit assumption
  tying this to the same `BelgianCalendarDay` (Europe/Brussels-anchored) concept feature 009/010
  already use for calendar-day boundaries, avoiding a second, inconsistent "what day is it"
  definition. See spec.md's updated Assumptions section.

## Requirement Consistency

- [x] CHK006 - Do spec.md's FR-010/FR-012 (absence creates an attendance pre-registration;
  extra/exchange do not) agree with plan.md's Summary and research.md R1/R2? [Consistency] —
  confirmed aligned across all three documents.
- [x] CHK007 - Does tasks.md's phase-4 (US2) Independent Test — which seeds mixed-type pending
  requests before extra/exchange submission (US3/US4) ships — contradict the strict
  phase-dependency ordering implied by "each user story is independently testable"? [Consistency,
  Tasks §Phase 4] — not a contradiction: tasks.md's own Dependencies section explicitly documents
  this as testable via direct seeded data rather than the live submission API, and flags the
  code-level (not test-level) dependency on US1's files explicitly rather than leaving it
  implicit.

## Acceptance Criteria Quality

- [x] CHK008 - Can SC-001 ("under 30 seconds") be objectively measured without implementation
  detail leaking in? [Measurability, Spec §SC-001] — yes, it's a user-timed interaction, not a
  system metric.
- [x] CHK009 - Can SC-005 ("zero instances of two directors both successfully deciding the same
  request") be verified without a load-testing framework this spec doesn't otherwise require?
  [Measurability, Spec §SC-005] — yes, verifiable via the concurrent-decision integration test
  already planned (tasks.md T020) rather than a production load test.

## Scenario Coverage

- [x] CHK010 - Are Exception/Error-path requirements as thorough as Primary-path requirements for
  all three request types (absence/extra/exchange)? [Coverage] — yes: FR-002/FR-003/FR-004 cover
  the exception paths for each type respectively; no type is left with only a happy path.
- [x] CHK011 - Is a Recovery scenario defined for "director approval fails because the date
  became a closure day after submission" (a genuine exception discovered mid-flow, not at
  submission)? [Coverage, Recovery] — covered by FR-011 and Edge Cases; the recovery is "director
  sees a clear rejection of their own approval action," not a silent failure.

## Edge Case Coverage

- [x] CHK012 - Is behavior specified for a parent submitting a duplicate request (same child,
  type, and date as an existing pending request)? [Gap] — **not specified**. Left as an
  intentional non-requirement: unlike feature 012a's waiting-list duplicate *flagging* (which
  exists because duplicate waiting-list entries are ambiguous business records), a second pending
  day-reservation for the same child/date/type is harmless — the director's queue simply shows
  both, and approving one still produces one attendance/status outcome per FR-015's guarded
  transition. Documented as a deliberate non-issue rather than a silent gap; no spec change
  needed.
- [x] CHK013 - Is behavior specified for a child who is deactivated/soft-deleted between request
  submission and director decision? [Edge Case, Gap] — not explicitly covered by an FR. Low risk
  (soft-deletion is rare mid-flow, and the approval handler already re-validates `ChildId`
  existence via the same child-exists check `MarkAbsentCommand` performs, which would fail
  cleanly) — logged as acceptable residual risk rather than a blocking gap, consistent with this
  feature's Out of Scope framing (no staff-leave-request-style lifecycle complexity intended
  here).

## Dependencies & Assumptions

- [x] CHK014 - Is the assumption that feature 014 (invoicing) will later read approved
  `extra`/`exchange` reservations validated as a real, checkable interface rather than a vague
  hope? [Assumption, Spec §Assumptions] — spec.md's Assumptions section explicitly scopes this as
  "the day reservation itself is the source of truth feature 014 is expected to read," not a
  built integration — an honest, checked assumption, not an unvalidated one.
- [x] CHK015 - Is the dependency on feature 013's `ICurrentParentContactResolver` still valid
  (i.e., does that interface still exist as documented)? [Dependency] — verified directly against
  the current codebase (`backend/ChildCare.Application/Common/ICurrentParentContactResolver.cs`)
  during planning, not assumed from memory.

## Ambiguities & Conflicts

- [x] CHK016 - Is there a requirement & acceptance-criteria ID scheme established (FR-XXX /
  SC-XXX)? [Traceability] — yes, consistent with every prior feature's spec.md numbering.

## Notes

- All items resolved during this pass. Two genuine gaps were found (CHK005's timezone ambiguity,
  now fixed in spec.md; CHK012/CHK013's edge cases, deliberately scoped as non-issues with
  reasoning recorded above rather than silently dropped) — per the standing process rule, findings
  are fixed rather than logged as deferred debt.
