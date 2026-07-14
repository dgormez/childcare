# Security & Tenant-Isolation Checklist: Platform-Admin Vaccine Catalog Management

**Purpose**: Validate that this feature's requirements are complete, unambiguous, and safe to
implement against — with emphasis on authorization-bypass and audit-trail correctness, since this
is the first cross-tenant admin authorization capability in the codebase.
**Created**: 2026-07-13
**Feature**: [spec.md](../spec.md)

**Note**: This checklist tests the requirements themselves (spec.md/plan.md/data-model.md), not
the eventual implementation.

## Authorization Requirement Completeness

- [x] CHK001 - Is the exact claim/flag check required to pass `PlatformAdminOnly` authorization documented at the requirements level (not just "has the flag"), including what happens if the claim is present but malformed (e.g. any value other than the exact expected string)? [Completeness, Gap]
- [x] CHK002 - Are requirements defined for what happens if a director's `IsPlatformAdmin` flag is revoked while they hold a still-valid (unexpired) access token — is a live-token check required, or is "eventually consistent by token expiry" an explicit, stated acceptance? [Gap, Spec Edge Cases]
- [x] CHK003 - Are requirements defined for every new endpoint individually (create/rename/reorder/deactivate/reactivate/list), or only asserted once at the feature level — could a future endpoint added under the same file silently ship without the same authorization requirement being independently verifiable? [Completeness, Spec §FR-009]
- [x] CHK004 - Is it specified whether `PlatformAdminOnly` requires `DirectorOnly` as a strict prerequisite (i.e., a `staff` or `parent` role token with a hypothetical `is_platform_admin` claim MUST also be rejected), or does the requirement only describe the intended-happy-path director case? [Ambiguity, Spec §FR-009]

## Tenant-Isolation Requirement Clarity

- [x] CHK005 - Does the spec's Technical Correction explicitly rule out any endpoint in this feature reading or writing tenant-schema domain data (beyond the `TenantUser.IsPlatformAdmin` flag itself), so a reviewer can verify Principle I compliance from the requirements alone without inspecting code? [Clarity, Spec Technical Correction]
- [x] CHK006 - Is it specified whether a platform-admin's own `tenant_id` (the tenant they happen to be a director of) has any bearing on which catalog entries they can act on — i.e., is it unambiguous that the catalog is fully tenant-agnostic for every platform-admin action, not just the read path 013g already shipped? [Clarity, Spec §FR-004, §FR-011]
- [x] CHK007 - Are requirements explicit that the `grant-platform-admin` CLI command must never write to more than one tenant schema per matching email (i.e., a bound on blast radius if an email happens to collide across tenants), or is "one email may exist in multiple tenants" an unaddressed scenario? [Gap, Edge Case]

## Audit-Trail Requirement Completeness & Measurability

- [x] CHK008 - Is "who deactivated it" specified precisely enough to be unambiguous — does it mean the acting platform-admin's account identity at the time of the action, and is it explicit that this must be captured server-side from the authenticated token rather than any client-supplied value? [Clarity, Spec §FR-008]
- [x] CHK009 - Are requirements defined for what a management-view consumer sees for `deactivatedByEmail` if the underlying `TenantUser` account is later deleted or its email changed — is denormalization-at-time-of-action (data-model.md) explicitly stated as the reason the audit record must remain stable regardless of later account changes? [Completeness, Data-Model §VaccineType]
- [x] CHK010 - Is the "fresh audit entry on re-deactivation" requirement (Spec §FR-008) explicit about whether the *previous* deactivation's who/when is retrievable anywhere after being overwritten, or does the spec clearly and intentionally accept that only the latest deactivation is ever visible (no history)? [Clarity, Ambiguity, Spec §FR-008]
- [x] CHK011 - Are audit-field requirements consistent between the User Story 3 acceptance scenarios and FR-008/FR-012 — do both describe the same "who/when, current-state-only" scope, or does either imply a broader history/log requirement the other doesn't? [Consistency, Spec §FR-008, §FR-012]

## Data-Integrity Requirement Coverage

- [x] CHK012 - Are concurrent-modification requirements specified for two platform-admins (or the same admin in two tabs) reordering or deactivating the same entry simultaneously, beyond the general "last write wins" reorder note — does that same resolution explicitly extend to deactivate/reactivate races, or is that scenario unaddressed? [Coverage, Gap, Spec Edge Cases]
- [x] CHK013 - Is the mutual-exclusivity between "entry is active" and "entry has non-null audit fields" stated as an invariant the requirements expect to always hold, or only implied by the individual FRs describing deactivate/reactivate in isolation? [Consistency, Ambiguity, Spec §FR-007, §FR-008]
- [x] CHK014 - Are requirements defined for whether a platform-admin action on the catalog can ever partially fail (e.g., the entry updates but the audit fields don't) in a way visible to a caller, or is atomicity of each action assumed but never stated? [Gap, Non-Functional]

## Dependencies & Assumptions Validation

- [x] CHK015 - Is the assumption that "013g's `GET /api/vaccine-types` behavior/shape/auth is unaffected" paired with a stated verification mechanism (a regression requirement), or only asserted as a constraint with no corresponding measurable check? [Traceability, Spec §FR-010]
- [x] CHK016 - Does the spec's Assumptions section's claim that the `IsPlatformAdmin` grant "MUST provide a repeatable, auditable way to perform this grant" define what "auditable" means here (e.g., command output, a persisted log, or something else) with enough precision to be objectively verified, or is it left to implementation discretion entirely? [Measurability, Ambiguity, Spec Assumptions]

## Notes

- Items marked [Gap] or [Ambiguity] should be resolved (spec/plan updated) before or during
  implementation — per this project's standing rule, findings are fixed, not deferred, even when
  advisory.
