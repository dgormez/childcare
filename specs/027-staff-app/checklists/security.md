# Security, Tenant-Isolation & Data-Safety Checklist: Staff App (Personal Rota & Leave)

**Purpose**: Validate that spec.md's requirements around authorization, cross-staff data
isolation, tenant isolation, and sensitive-data handling are complete, unambiguous, and
consistent — before implementation. Same emphasis as prior features' security-focused checklist
passes (013g, 014a, 025, 026).
**Created**: 2026-07-22
**Feature**: [spec.md](../spec.md)

## Authorization & Identity Resolution

- [x] CHK001 Are requirements explicit that schedule/leave-request **reads** always resolve
      staff identity from the JWT rather than a client-supplied parameter? [Completeness,
      Spec §FR-015]
- [x] CHK002 Are requirements explicit that schedule/leave-request **writes** (sick report,
      leave-request submission) also always resolve staff identity from the JWT, symmetric with
      the read-side guarantee in FR-015? [Fixed — Spec §FR-015a added]
- [x] CHK003 Are the authorization policies for each new endpoint (director-only vs.
      staff-or-director) unambiguous? [Clarity, Spec §Technical Requirements — Security
      considerations]
- [x] CHK004 Is the requirement that cover-assignment writes continue to enforce
      `StaffLocationEligibility` stated as a MUST, not a should? [Clarity, Spec §FR-014]

## Cross-Tenant & Cross-Staff Isolation

- [x] CHK005 Is a requirement present preventing a staff member from reading another staff
      member's schedule or leave requests? [Completeness, Spec §FR-015]
- [x] CHK006 Is multi-tenant isolation (no new cross-tenant surface) explicitly addressed for
      the new `StaffLeaveRequest` entity, not just `StaffSchedule`? [Coverage, Spec §Technical
      Requirements — Security considerations]

## Sensitive-Data Handling

- [x] CHK007 Does the spec define who may view a leave request's free-text `Notes` field, given
      a `sick`-typed request's notes could contain employee health information (a GDPR special
      category)? [Fixed — Spec §Technical Requirements, Security considerations, added]
- [x] CHK008 Are retention requirements defined for leave-request notes, distinct from the
      general assignment data they're attached to? [Fixed — same edit; explicitly no separate
      retention, deferred to feature 038's general policy]
- [x] CHK009 Is the requirement that internal errors/stack traces are never exposed to
      end users addressed for this feature's new endpoints (inherits the constitution-wide
      rule)? [Consistency, Constitution Principle VI — no feature-specific exception claimed]

## Concurrency & State-Mutation Safety

- [x] CHK010 Is a race-safety requirement defined for concurrent cover-assignment attempts on
      the same absence? [Completeness, Spec §FR-018]
- [x] CHK011 Is the interaction between a leave-request approval (FR-011, bulk-marks a date
      range `Absent`) and a `StaffSchedule` row that is already `Covered` or `Confirmed` at
      approval time explicitly specified — does approval overwrite a `Covered` row's cover
      assignment, or skip it? [Fixed — Spec §FR-011a added: `Covered` rows are skipped, never
      overwritten]
- [x] CHK012 Is a rate-limit or idempotency requirement defined for the one-tap "Ik ben ziek"
      action, given it's a state-mutating, low-friction, single-tap endpoint reachable
      repeatedly? [Fixed — Spec §FR-005a added: idempotent, no duplicate request/alert]

## Consistency With Existing Decoupling Guarantees

- [x] CHK013 Is the requirement that the live BKR ratio computation must never read the new
      `Status`/`CoverStaffId` fields stated as testable (a specific regression test named)?
      [Measurability, Spec §FR-016]
- [x] CHK014 Are the requirements for `StaffLocationEligibility` enforcement consistent between
      the existing create/update paths (feature 012) and the new cover-assignment path?
      [Consistency, Spec §FR-014]

## Notification Content Safety

- [x] CHK015 Does the spec define what information a push notification may or may not include
      (e.g., must a "schedule changed" notification avoid naming which colleague is now
      covering, if that itself could be sensitive)? [Fixed — Spec §FR-008a added]

## Assumptions & Dependencies

- [x] CHK016 Is the assumption that no production tenant data exists yet (justifying an in-place
      migration rather than a dual-write transition) explicitly stated and validated?
      [Traceability, Spec §Assumptions]
- [x] CHK017 Are this feature's dependencies on existing security-relevant mechanisms
      (`StaffLocationEligibility`, JWT `role=staff` auth, `IAdvisoryLockService`) explicitly
      listed rather than assumed silently? [Traceability, Spec §Assumptions]

## Notes

- CHK002, CHK007, CHK008, CHK011, CHK012, CHK015 were genuine gaps on first pass — all six
  fixed in spec.md (FR-015a, FR-005a, FR-011a, FR-008a, and a Notes-field visibility/retention
  paragraph under Technical Requirements) before proceeding, per this pipeline's standing rule
  to fix every finding rather than log it as debt. 17/17 items now pass.
- All other items were already satisfied by the existing spec text.
