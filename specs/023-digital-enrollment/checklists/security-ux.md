# Security & UX Requirements Checklist: Digital Online Enrollment

**Purpose**: Validate the quality (completeness, clarity, consistency, measurability) of this
feature's anti-abuse/security requirements and its public-facing UX requirements before
implementation. Focus chosen (autonomous run, no interactive user available): the two highest-
risk requirement clusters for this feature — an unauthenticated, spam-exposed public endpoint,
and a parent-facing form/email flow with locale and lifecycle edge cases. Depth: Standard.
Audience: Reviewer (PR).
**Created**: 2026-07-21
**Feature**: [spec.md](../spec.md)

## Anti-Abuse & Public-Endpoint Security Requirement Quality

- [x] CHK001 Is the submission rate limit quantified with a specific count and window rather
  than left as "rate-limited"? [Clarity, Spec §FR-006 — "3 per source IP address per rolling
  hour"]
- [x] CHK002 Is the honeypot rejection's observable behavior specified precisely enough to test
  (no entry created, but the same success response shape returned) rather than left as "silently
  discarded"? [Clarity, Spec §FR-005]
- [x] CHK003 Is the reference code's format quantified (length, character set) rather than left
  as an unquantified "short, human-legible" adjective? [Clarity, Spec §FR-008 — resolved in this
  pass: 8 characters, excluding 0/O/1/I/l, added via a second Clarifications entry]
- [x] CHK004 Is the boundary on what tenant/child/contact data the public, unauthenticated
  endpoints may expose stated explicitly, rather than left implicit from "no login required"?
  [Completeness, Spec §FR-021]
- [x] CHK005 Is server-side enforcement of the disabled-location state (not just a hidden UI
  form) stated as its own requirement, distinct from the UI behavior? [Completeness, Spec
  §FR-013]
- [x] CHK006 Is the terminal-status guard on tour-invitation responses (an accept/decline click
  arriving after the entry is already `Enrolled`/`Withdrawn`) specified as a requirement, not
  just an implementation detail left to research.md? [Completeness, Spec §FR-018]
- [x] CHK007 Does the spec avoid prescribing a specific token/signing mechanism for the
  tour-invitation link (the "how"), consistent with treating that as a planning-phase decision?
  [Consistency, Spec §Technical Requirements — mechanism left to research.md]

## Public-Form & Email UX Requirement Quality

- [x] CHK008 Is duplicate-entry handling specified as "flag, never auto-reject" with the
  reasoning stated, rather than left ambiguous between flagging and rejecting? [Clarity, Spec
  §FR-011/Edge Cases]
- [x] CHK009 Is the language/locale used for each outbound email (confirmation, tour invitation)
  traceable to a specific rule for every entry origin — including director-entered entries,
  which have no submitted-form language to fall back to? [Gap — resolved in this pass: FR-015
  now states the fallback to the location's default enrollment language for entries with no
  submitted language]
- [x] CHK010 Is the behavior specified for sending a second tour invitation to an entry that
  already has one (reschedule/re-propose), rather than only covering the first send? [Gap —
  resolved in this pass: FR-015 now states a new invitation replaces the prior proposed date/
  time and resets the accept/decline status]
- [x] CHK011 Are loading, inline-validation, rate-limited, and disabled-location states each
  specified as distinct UI states for the public form, rather than one undifferentiated "error"
  case? [Completeness, Spec §UX Requirements/Loading-empty-error states]
- [x] CHK012 Is the required-vs-optional status of every public form field (in particular
  contact email, which differs from 012a's director-entered flow) stated explicitly rather than
  left to be inferred from the field list alone? [Clarity, Spec §FR-003/Edge Cases]

## Consistency & Traceability

- [x] CHK013 Do the Success Criteria (SC-002, SC-004) and the Functional Requirements (FR-002,
  FR-006) agree on the default-disabled and rate-limit numbers, with no wording gap between the
  two sections? [Consistency, Spec §Success Criteria vs §Functional Requirements]
- [x] CHK014 Is every rejection/error case enumerated in the API contract
  (contracts/enrollment-api.md) traceable back to a specific FR in spec.md, with no contract-
  only error case lacking a corresponding requirement? [Traceability, Spec §Functional
  Requirements vs contracts/enrollment-api.md]

## Notes

- 14/14 items pass. CHK003, CHK009, and CHK010 were genuine completeness/clarity gaps (not
  defects in existing text) and were closed in this same pass by editing spec.md: a second
  Clarifications entry quantifying the reference code's format, and an expansion of FR-015
  covering re-send/reschedule behavior and the locale fallback for director-entered entries.
  research.md R5 and contracts/enrollment-api.md's tour-invitation section already implemented
  the same resolutions independently — this pass brings spec.md's FR text in line with them
  rather than introducing a new decision.
- Ready for `/speckit-analyze`.
