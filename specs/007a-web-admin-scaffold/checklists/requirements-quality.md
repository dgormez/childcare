# Specification Quality Checklist: Web Admin Scaffold — Requirements Quality & Implementation Readiness

**Purpose**: Validate that spec.md/plan.md/tasks.md are complete, unambiguous, and consistent
enough to implement without further clarification.
**Created**: 2026-07-08
**Feature**: [spec.md](../spec.md)
**Depth**: Standard (pre-implementation gate, reviewed before `/speckit-analyze`)
**Audience**: Implementer (this session) / future reviewer

## Requirement Completeness

- [x] CHK001 - Are requirements defined for what happens when a director's Google account is
  valid but not linked to any organisation on this tenant? [Completeness, Spec §FR-002] —
  covered by feature 003's existing link-only OAuth behavior (shipped-notes), not re-specified
  here since this feature reuses that contract unchanged; no gap.
- [x] CHK002 - Are requirements defined for the maximum/expected data volume the Staff and
  Devices tables must remain usable at? [Completeness, Spec §Technical Requirements /
  Assumptions] — "tens to low hundreds" is stated in Assumptions and SC-002.
- [x] CHK003 - Are requirements defined for how a director without any assigned locations (a
  brand-new tenant) experiences the Staff/Devices screens? [Completeness, Spec §FR-011, FR-015]
  — covered by the empty-state requirements (FR-011/FR-015), which apply regardless of cause.
- [x] CHK004 - Is there a requirement covering what the sidebar displays before the
  organisation-name/director-name data has loaded (initial render, before the async call
  resolves)? [Gap] → **Resolved**: added FR-005b (loading-state requirement) — see Resolution
  Log below.
- [x] CHK005 - Are requirements defined for keyboard-only completion of the PIN-reset and
  revoke-confirmation flows (not just "keyboard-navigable," but that the confirm action itself
  is reachable without a mouse)? [Completeness, Spec §UX Requirements/Accessibility] — covered:
  "keyboard-navigable table and nav, visible focus states" is general-purpose and dialogs
  inherit standard focus-trap expectations; no dialog-specific carve-out needed at this
  precedent level (matches how 008a's spec treats accessibility as a general cross-cutting
  requirement, not enumerated per-dialog).

## Requirement Clarity

- [x] CHK006 - Is "a few seconds" (SC-002, staff search) quantified precisely enough to be
  objectively verified? [Clarity, Spec §SC-002] — quantified as "under 5 seconds"; clear.
- [x] CHK007 - Is "3 clicks or fewer" (SC-003/SC-004) clearly scoped to start from the
  list-view row action, so it's unambiguous where the click count begins? [Clarity, Spec
  §SC-003] — text explicitly states "from the staff list"/"from the devices list"; clear.
- [x] CHK008 - Is "clear, human-readable inline error state" (FR-012/FR-016) specific enough to
  distinguish it from a toast/banner/modal, or is the exact presentation left to design? [Clarity,
  Spec §FR-012] — intentionally left to design/plan level (research.md/plan.md's `ErrorState.tsx`
  primitive), consistent with the spec template's "WHAT not HOW" guidance; not a defect.

## Requirement Consistency

- [x] CHK009 - Do FR-020's "no new backend endpoint" framing and FR-013a/FR-005a's explicit
  exceptions read as consistent, or does FR-020 need to be read carefully to avoid appearing
  self-contradictory? [Consistency, Spec §FR-020] → **Resolved**: FR-020 already cross-references
  both exceptions by ID inline; re-read and confirmed unambiguous, no edit needed.
- [x] CHK010 - Does User Story 3's "disappears from (or is visually distinguished in) the active
  devices list" (Acceptance Scenario 2) leave the choice between "disappears" and "visually
  distinguished" fully open, and is that intentional given FR-013a describes returning revoked
  devices in the same list? [Consistency, Spec §US3 Acceptance Scenario 2, FR-013a] — intentional:
  FR-013a's response includes `revokedAt` for all devices (never filters), so "visually
  distinguished" is the only reading consistent with FR-013a; the acceptance scenario's
  either/or phrasing is a minor looseness worth tightening. → **Resolved**: see Resolution Log.
- [x] CHK011 - Is the "Director" role terminology used consistently with feature 005's
  "Director-role account with an optional Staff Profile" distinction, particularly for the Staff
  table's "role" column when a director also has a staff profile? [Consistency, Spec §FR-007] —
  Key Entities section correctly scopes "Staff" to feature 005's entity; FR-007 doesn't specify
  display format for dual-role rows, which is a legitimate implementation-level detail (table
  cell rendering), not a requirements gap given feature 005 already defines the underlying role
  model precisely.

## Acceptance Criteria Quality

- [x] CHK012 - Can SC-001 ("under 10 seconds ... on a typical broadband connection") be
  objectively measured without further definition of "typical broadband"? [Measurability, Spec
  §SC-001] — acceptable as a qualitative performance target at this feature's precedent level
  (matches how other shipped features phrase non-critical-path performance goals); not a
  regulatory or safety-critical metric requiring stricter quantification.
- [x] CHK013 - Is SC-007 ("verified by testing with at least two distinct tenant accounts")
  written as a testable acceptance criterion rather than a test-plan instruction? [Clarity, Spec
  §SC-007] — phrasing is borderline (reads as a test method) but expresses a genuine, measurable
  outcome (zero cross-tenant leakage, verifiable via that method); consistent with how tenant-
  isolation success criteria are phrased elsewhere in this codebase's specs. No change needed.

## Scenario Coverage

- [x] CHK014 - Are requirements defined for the scenario where a director's session is valid but
  their role has been changed/downgraded server-side between page loads? [Gap] — out of scope:
  no feature in this codebase supports changing a TenantUser's Role post-creation (confirmed via
  feature 005's shipped-notes: "no role-change capability exists anywhere"); not a gap in this
  spec, the scenario cannot occur given current system behavior.
- [x] CHK015 - Are requirements defined for concurrent PIN-reset attempts on the same staff
  member from two director sessions? [Coverage, Edge Case] — covered by feature 008a's existing
  `errors.pin.not_unique_at_location` conflict semantics (this spec's Edge Cases section
  references surfacing that error); the underlying concurrency behavior is 008a's contract, not
  re-specified here, consistent with FR-020's "consumer only" framing.
- [x] CHK016 - Are requirements defined for what a director sees if they navigate directly to a
  placeholder nav entry's URL (if one exists) rather than clicking a disabled link? [Gap] →
  **Resolved**: added an Edge Case bullet — see Resolution Log.

## Non-Functional Requirements

- [x] CHK017 - Are localization requirements (FR-017) explicit about whether locale is
  auto-detected, user-selected, or both? [Clarity, Spec §FR-017] → **Resolved**: added an
  Assumption clarifying locale-selection mechanism is out of this feature's scope (reuses
  whatever mechanism next-intl's setup provides; no new UI for locale switching is required by
  this feature) — see Resolution Log.
- [x] CHK018 - Are accessibility requirements specific enough to be verified without a screen
  reader test plan being written first (i.e., do they name concrete, checkable properties)?
  [Measurability, Spec §UX Requirements/Accessibility] — "keyboard-navigable," "visible focus
  states," "form labels on all inputs," "sufficient contrast per design-system.md tokens" are
  each independently checkable; sufficient for this feature's precedent level.
- [x] CHK019 - Are performance requirements for the client-side table filter (SC-002) decoupled
  from network latency, so a slow backend response doesn't get conflated with a slow filter
  operation? [Clarity, Spec §SC-002] — SC-002 measures "locate a specific staff member ... using
  search/filter," which is explicitly the post-load, client-side operation, distinct from SC-001
  (initial load time); no ambiguity.

## Dependencies & Assumptions

- [x] CHK020 - Is the assumption that "backend endpoints ... are complete, stable, and require no
  changes" still accurate after this spec's own FR-013a/FR-005a exceptions were added? [Conflict,
  Spec §Assumptions] — the Assumptions bullet was already updated in-line to carve out both
  exceptions explicitly (confirmed by re-reading the current spec text); no residual
  contradiction.
- [x] CHK021 - Is the dependency on `next-intl` (and the resulting removal of `web/`'s current
  lack of any i18n mechanism) called out as a dependency, or only implied by FR-017? [Gap] —
  library choice is a plan-level detail (plan.md's Primary Dependencies), correctly not named in
  spec.md per the spec template's "WHAT not HOW" rule; FR-017 states the requirement
  technology-agnostically. Not a spec defect.
- [x] CHK022 - Is the assumption that placeholder nav entries require no routing/URL
  reservation validated against Next.js App Router's file-based routing (i.e., could a
  future feature's route collide with a placeholder label)? [Assumption] — this is an
  implementation/plan-level concern (tasks.md T024 renders placeholders as non-navigating
  list items, not routes), correctly not addressed at the spec level.

## Ambiguities & Conflicts

- [x] CHK023 - Is the term "confirmation step" (FR-010, FR-014) consistently understood as a
  blocking modal dialog rather than an inline undo-able action, across both Staff and Devices?
  [Consistency, Spec §FR-010, FR-014] — both FRs use identical "with an explicit confirmation
  step before the action is applied" phrasing; consistent. The specific UI pattern (modal vs.
  inline) is correctly deferred to plan/design level (`ConfirmDialog.tsx`).
- [x] CHK024 - Is there a conflict between "no offline-first behavior is required" (Assumptions)
  and the general product principle (design-system.md) of graceful degradation? [Conflict] — no
  conflict: design-system.md's principles apply per-surface, and platform-rules.md scopes
  offline capability to the caregiver tablet only; director web's assumption is consistent with
  documented platform differentiation, not a deviation from it.

## Resolution Log

- **CHK004** → Added **FR-005b**: "While organisation/director name data is loading, the
  navigation shell MUST show a neutral loading state (e.g., skeleton text) rather than blank
  space or a flash of empty/placeholder text." (see spec.md)
- **CHK010** → Tightened User Story 3, Acceptance Scenario 2's wording from "disappears from (or
  is visually distinguished in)" to "is visually distinguished in (not removed from)" to match
  FR-013a's response shape, which always includes revoked devices.
- **CHK016** → Added an Edge Case bullet: "What happens if a director navigates directly to a
  not-yet-built section's URL (bypassing the disabled nav entry)? The system MUST show a
  not-yet-available message within the shell, not a broken route or a raw 404."
- **CHK017** → Added an Assumption: "Locale selection/detection mechanism (browser-detected vs.
  explicit switcher) is out of this feature's scope to design new UI for — this feature only
  guarantees translated content exists for whatever mechanism `next-intl` is configured with;
  adding a visible locale switcher is a future feature's decision if needed."

## Notes

- 24 items generated; 4 surfaced genuine gaps/looseness and were resolved directly in spec.md
  (this feature's process requires fixing findings, not deferring them — see
  `.specify/memory/process-next-feature.md`). The remaining 20 were checked against the current
  spec/plan text and confirmed already adequate, each with its reasoning recorded above so a
  reviewer doesn't need to re-derive it.
