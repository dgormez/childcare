# Security Requirements Quality Checklist: Caregiver App Kiosk Mode

**Purpose**: Validate requirements quality for device-token/PIN auth composition,
revocation/rotation correctness, and the shared-lockout security model
**Created**: 2026-07-08
**Feature**: [spec.md](../spec.md)

## Device-Token / PIN Auth Composition

- [x] CHK001 - Does FR-004 specify group-level scope validation, or only tenant/location — given the device token also carries `group_id` per FR-002/Key Entities? [Completeness, Spec §FR-002, §FR-004]
- [x] CHK002 - Is it specified whether a caregiver's PIN must belong to someone *currently checked in* to be valid at the FR-017 sensitive-action confirmation step, or whether any caregiver's valid PIN suffices regardless of shift state? [Gap, Spec §FR-017]
- [x] CHK003 - Are the two auth layers' failure-mode interactions specified — e.g., does a request with a valid-but-unpaired-caregiver PIN and an invalid device token fail for the device-token reason or the PIN reason, and does the response distinguish them? [Consistency, Spec §FR-003, §FR-009]

## Revocation / Rotation Correctness

- [x] CHK004 - Is the precedence between FR-020 (silent rotation) and FR-021 (revocation) specified for the case where both conditions are true on the same request (a token that is both near-expiry and revoked)? [Conflict, Spec §FR-020, §FR-021]
- [x] CHK005 - Is device-signing-key-level compromise (as opposed to a single device token/tablet) addressed, even as an explicit out-of-scope statement? [Gap]
- [x] CHK006 - Is the auto-checkout correction in FR-023 required to be audit-logged (who corrected, when, prior value) with the same rigor US7's revoked-tablet sync rejections are (FR-021's "logged server-side for audit")? [Consistency, Spec §FR-023, Edge Cases]

## Shared-Lockout Security Model

- [x] CHK007 - Is "5 failures within 2 minutes" (FR-012) specified as a sliding window or a fixed window — these produce materially different lockout behavior and the spec doesn't disambiguate? [Ambiguity, Spec §FR-012]
- [x] CHK008 - Is the *response*, not just the persisted state, specified for a request that arrives *during* an active lockout window versus one that triggers the lockout — do both return the same status/error shape? [Clarity, Spec §FR-012]
- [x] CHK009 - Is it specified whether the director-override PIN (FR-005) is rate-limited at all, given it is explicitly *not* part of the shared caregiver-PIN lockout counter (Clarifications)? [Gap, Spec §FR-005, Clarifications]

## Notes

- All nine items above are genuine requirements-quality gaps, not implementation questions —
  each was resolved by editing `spec.md` directly rather than left open; see the updated FRs
  and a new Clarifications entry for CHK007's sliding-vs-fixed-window resolution.
- CHK001, CHK002, and CHK006's spec fixes (FR-004 group scope, FR-017 checked-in requirement,
  FR-023 correction audit logging) additionally required new/updated tasks in `tasks.md`
  (group-scope test, checked-in-requirement test, and a `CorrectShiftCommand` + `PATCH
  /api/room-shifts/{id}` endpoint that had no task at all before this pass) and matching
  updates to `contracts/room-shift-api.md` and `data-model.md` — a checklist finding that only
  patches the prose without closing the downstream artifact gap isn't actually fixed.
- **Post-implementation-prep revision**: the caregiver identification UX changed from PIN-only
  to select-then-PIN (BACKLOG.md's revision, confirmed against Procare/KinderSign) — every
  PIN-verifying call now carries an explicit `staff_id`. This directly simplified CHK002's and
  CHK007–009's resolutions (lockout is now a simple per-`StaffProfile` counter, not a
  value-keyed one — research.md R2/R6) without reopening the underlying findings: all nine
  items above still hold under the revised design, just with a simpler mechanism than the
  PIN-only draft required.
