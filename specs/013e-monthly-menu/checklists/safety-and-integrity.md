# Safety, Tenant-Isolation & Data-Integrity Checklist: Monthly Menu

**Purpose**: Validate requirements quality for this feature's three highest-risk dimensions:
parent-facing authorization scoping (`ChildContacts`, plus a brand-new parent-facing closure-day
read with no existing precedent), data-integrity of the director-approval write-through to the
existing `MealPreference` entity, and tenant isolation of both new director- and parent-facing
surfaces.
**Created**: 2026-07-14
**Feature**: [spec.md](../spec.md)

**Depth**: Standard. **Audience**: Reviewer (this feature's own implementer, acting as reviewer
per the standing single-pass process). **Focus**: safety/authorization, data integrity, tenant
isolation — UI polish and performance are out of scope for this checklist.

## Parent-Facing Authorization Scoping

- [x] CHK001 Does the spec explicitly forbid a parent from ever seeing a published menu for a location none of their linked children holds an active contract at, not just describe which locations they *should* see? [Gap, Spec §FR-006]
- [x] CHK002 Is the closure-day read this feature introduces (the first parent-scoped closure-calendar read in this codebase, per Technical Requirements) bounded to only the date/label fields a parent needs, with an explicit requirement that it must never expose the director-only delivery-status data the existing `DirectorOnly` closure read carries? [Completeness, Spec §Technical Requirements]
- [x] CHK003 Does the spec state that the closure-day read's location scope is derived server-side from the requesting parent's own active contracts, rather than accepting an arbitrary `locationId` the client supplies? [Ambiguity, Spec §FR-006]
- [x] CHK004 Is there a requirement explicitly forbidding the director-web preference-request review queue (FR-013) from ever returning a request belonging to a different tenant? [Gap, Spec §FR-017]

## Data-Integrity of the Approval Write-Through

- [x] CHK005 Does the spec state precisely which `child_meal_preferences` fields an approval updates versus leaves untouched — i.e. does approving a texture-only request preserve the child's existing dietary tags (and vice versa), rather than clobbering unrequested fields to empty/default? [Clarity, Spec §FR-014]
- [x] CHK006 Does the spec define the outcome when two preference-change requests for different children are submitted concurrently and both pass the "no existing pending request" check before either is persisted — is a race here acceptable, or must it be prevented? [Edge Case, Spec §FR-012]
- [x] CHK007 Does the spec define what happens to a `Pending` preference-change request if the target child is deactivated/departs before a director decides it? [Gap, Spec Edge Cases]
- [x] CHK008 Is simultaneous-decision handling (two directors approving/rejecting the same request at the same moment) addressed, consistent with how this spec's own precedent feature (013a) treats concurrent decision attempts? [Gap, Spec §FR-014, FR-015]

## Tenant Isolation

- [x] CHK009 Does the spec state that every new director-web menu-authoring and preference-review endpoint operates only within the acting director's own tenant, not merely "the director's own tenant" as a general aside? [Consistency, Spec §FR-017]
- [x] CHK010 Are the boundaries between what a parent can read (published menus, own child's preference/requests) and what a director can read (all requests, draft+published menus) stated precisely enough that a reader could not confuse which authorization group a new requirement belongs to? [Clarity, Spec §FR-002, FR-003, FR-013]
- [x] CHK011 Does the spec explicitly confirm that neither surface is reachable by a caregiver device-token session, given this is the first feature since 008a to introduce parent-app endpoints with no caregiver-tablet equivalent at all? [Completeness, Spec §FR-019]

## Notes

- All 11 items were checked against the current spec.md during this same pass (not deferred) —
  per the standing process rule, every finding is fixed in the spec now rather than logged as
  unaddressed debt.
- **Findings and fixes applied**: CHK001, CHK002, CHK003, CHK005, CHK006, CHK007, and CHK008
  surfaced genuine gaps; each was resolved by tightening the relevant FR/Edge Case/Assumption
  wording in spec.md directly. CHK004, CHK009, CHK010, CHK011 were already adequately covered by
  existing tenant-isolation/authorization language and required no change.
