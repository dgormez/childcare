# Security & UX Requirements Checklist: QR Contactless Check-In

**Purpose**: Validate the quality (completeness, clarity, consistency, measurability) of this
feature's security/tamper-evidence and UX/offline-edge-case requirements before implementation.
Focus chosen (autonomous run, no interactive user available): the two highest-risk requirement
clusters for this feature — a forgeable/replayable check-in credential, and an offline/degraded
scan flow that could block attendance. Depth: Standard. Audience: Reviewer (PR).
**Created**: 2026-07-20
**Feature**: [spec.md](../spec.md)

## Security & Tamper-Evidence Requirement Quality

- [x] CHK001 Is the code's validity window quantified with a specific duration rather than left
  as "short"? [Clarity, Spec §FR-006 — resolved to 30s via Clarifications session]
- [x] CHK002 Is "tamper-evident" given a testable acceptance criterion (reject any
  altered/unofficial code) rather than left as an unquantified adjective? [Measurability, Spec
  §FR-007]
- [x] CHK003 Is the child-ownership boundary for code issuance (a parent may only request a code
  for their own linked child) explicitly stated as a requirement, not just implied by platform
  convention? [Completeness, Spec §Technical Requirements/Security considerations]
- [x] CHK004 Are the location-boundary check (FR-010) and the tamper-evidence check (FR-007)
  specified as producing distinct, separately identifiable rejection outcomes rather than one
  generic "invalid" error? [Clarity, Spec §FR-010/FR-011]
- [x] CHK005 Is there a requirement covering what stops a captured/replayed code (e.g. a photo of
  the phone screen) from being reused after the original scan already succeeded, distinct from
  the passive TTL expiry alone? [Coverage, Spec §FR-019 — resolved via Clarifications session's
  post-consumption cooldown]
- [x] CHK006 Is the source of the signing/verification key that makes codes tamper-evident
  specified as server-side-only and never client-shipped, anywhere in the requirements
  (spec.md or plan.md), or does this only appear in research.md's implementation notes? [Gap —
  resolved: spec.md's Technical Requirements/Security considerations now states this explicitly
  and ties it to why offline verification is impossible (FR-012)]
- [x] CHK007 Does the spec avoid stating or implying a specific cryptographic mechanism (the
  "how"), consistent with its own Assumptions note that the mechanism is a planning-phase
  decision? [Consistency, Spec §Assumptions]

## Offline & Degraded-Path UX Requirement Quality

- [x] CHK008 Is caregiver-tablet behavior specified separately for two distinct offline
  conditions — (a) fully offline at the moment of attempting a scan, and (b) connectivity lost
  after a scan is verified but before the resulting attendance write completes — rather than one
  undifferentiated "offline" case? [Completeness, Spec §FR-012 — resolved during planning;
  originally a single undifferentiated case]
- [x] CHK009 Is the camera-unavailable/camera-failure fallback path (FR-013) specified with the
  same rigor (a defined, reachable next step) as the offline path, rather than only asserting
  "manual tap remains available" in the abstract? [Clarity, Spec §FR-013/User Story 3]
- [x] CHK010 Is the parent-app behavior specified for the case where the app is offline when a
  code would normally be (re)issued, including whether this is visually distinguishable from a
  caregiver-side scan rejection? [Coverage, Spec §UX Requirements/Offline behavior]
- [x] CHK011 Is the specific UX consequence of FR-019's re-scan cooldown specified for the
  caregiver (what they see if a code is scanned again too soon), or does the requirement only
  describe the system-side rejection without describing what the caregiver-facing outcome looks
  like? [Gap — resolved: UX Requirements' rejection-message enumeration now covers this as a
  fourth, distinct non-error "already processed" state]
- [x] CHK012 Are loading-state requirements specified for both the parent's code-issuance/refresh
  cycle and the caregiver's scan-to-confirmation window, so neither surface is left with an
  undefined blank/frozen state under normal latency? [Completeness, Spec §UX
  Requirements/Loading-empty-error states]

## Consistency & Traceability

- [x] CHK013 Do the Success Criteria (SC-002, SC-005) and the Functional Requirements (FR-004,
  FR-013) agree on what "unaffected" and "no additional steps" mean for a location with the
  setting disabled, without any wording gap between the two sections? [Consistency, Spec
  §Success Criteria vs §Functional Requirements]
- [x] CHK014 Is every rejection/error case enumerated in the API-facing contract
  (contracts/qr-checkin-api.md) traceable back to a specific FR in spec.md, with no
  contract-only error case lacking a corresponding requirement? [Traceability, Spec
  §Functional Requirements vs contracts/qr-checkin-api.md]

## Notes

- All 14 items pass. CHK006 and CHK011 were initially requirements-completeness gaps (missing
  explicit statements, not defects in existing text) and were closed in the same pass by editing
  spec.md: folding the server-only-key constraint into Technical Requirements/Security
  considerations, and adding the FR-019 cooldown case's caregiver-facing state to the UX
  Requirements' rejection-message enumeration.
- Ready for `/speckit-analyze`.
