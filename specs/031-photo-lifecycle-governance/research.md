# Research: Photo Lifecycle & Governance

## R1 — Tenant isolation vs. a bucket-wide GCS lifecycle rule

**Question**: The shared bucket (`{project}-staff-profile-photos`) has no tenant segment in any
object path (confirmed by reading `GcsProfilePhotoStorage.cs`, `GcsGroupActivityPhotoStorage.cs`,
`GcsHealthAttachmentStorage.cs`) — isolation relies on globally-unique GUIDs plus signed URLs,
not a path/bucket boundary. Does a bucket-wide Terraform `lifecycle_rule` (FR-005/FR-012, the
general 90-day cost-tiering policy) violate Constitution Principle I?

**Decision**: No — proceed with a single bucket-wide, prefix-scoped `lifecycle_rule`.

**Rationale**: Principle I's structural requirement is about *data leakage* (one tenant reading
or being served another tenant's data). A storage-class transition is not a read and does not
change what a signed URL resolves to or who can obtain one — it only changes retrieval cost/
latency for whichever object is transitioned, uniformly, regardless of tenant. There is no
code path where a tenant sees another tenant's object as a result of this rule. The rule is
prefix-scoped (`children/`, `staff/`, `group-activities/`, `health-records/`,
`vaccine-records/`) to exclude thumbnails and any non-photo prefix already sharing the bucket
(fiscal attestations, feature 015; bulk-email attachments, feature 020 — both explicitly out of
scope per spec.md's Clarifications), but not per-tenant, because GCS lifecycle conditions cannot
express "objects belonging to tenant X" (no tenant segment exists to match against).

**Alternatives considered**:
- *Add a tenant segment to every object path* — would make tenant-scoped lifecycle rules
  possible, but is a breaking change to every existing storage port's path convention, explicitly
  out of scope for this feature ("no new upload/storage mechanism," spec.md constraints). Rejected.
- *Per-tenant lifecycle policy (e.g. different tiering windows per plan tier)* — no product
  requirement for this exists yet (spec.md's grace periods are flat defaults); would require the
  tenant-segment change above anyway. Rejected as speculative.

## R2 — Archive-on-departure cannot be a native GCS lifecycle rule

**Question**: Can the 30-day post-deactivation archive-to-Coldline policy (FR-002/FR-003) be
expressed as a GCS-native `lifecycle_rule`?

**Decision**: No. GCS Object Lifecycle Management conditions are limited to `age` (days since
object creation), `createdBefore`, `customTimeBefore`, `daysSinceCustomTime`,
`daysSinceNoncurrentTime`, `noncurrentTimeBefore`, `isLive`, `matchesStorageClass`,
`numNewerVersions`, `matchesPrefix`/`matchesSuffix` — none of these can express "every child
derived from this object is now inactive," which depends on live application/business state
(`Child.DeactivatedAt`, group-membership history) that GCS has no visibility into. This
confirms spec.md's own note that the eligibility check "needs an explicit signal."

Instead: a new app-level scheduled job (`evaluate-photo-archival`, a CLI subcommand following the
exact shape of `SendPaymentRemindersCommand`/`SendDailyReportsCommand`) computes eligibility
per-tenant from `Child.DeactivatedAt` and the shared group-membership derivation (R3), then
issues a direct GCS storage-class update (`Google.Cloud.Storage.V1`'s `StorageClient.UpdateObject`
with `Object.StorageClass` set to `COLDLINE`) for each eligible object — no full object rewrite/
copy needed, since GCS supports an in-place storage-class PATCH via the JSON API.

**Alternatives considered**:
- *Use `customTime` as a signal GCS can act on*: have the app set/refresh a `customTime` metadata
  field the moment a child is deactivated, then a native `daysSinceCustomTime: 30` lifecycle rule
  transitions it automatically with no app-level job needed for the transition itself (only for
  setting `customTime`). Rejected for the *group-activity* case specifically: a photo's
  eligibility depends on **every** derived child being inactive, which can change repeatedly
  (last remaining active child eventually leaves too) — `customTime` would need to be reset each
  time the eligible set changes, which is no simpler than just running the transition directly,
  and splitting "set a metadata hint" from "GCS eventually acts on it" adds a day(s)-long lag with
  no benefit over doing the transition immediately in the same job run. Rejected for unnecessary
  indirection.

## R3 — Reuse the existing group-membership derivation, don't duplicate it

**Question**: `GetParentGroupActivityGalleryQuery` already computes, inline, which children a
parent should see a group-activity photo for (via `ChildGroupAssignments`, matched to the
activity's group and date, per spec.md's Clarifications). The archival job and the purge command
both need the same "which children does this photo depict" answer. Duplicate the logic or share
it?

**Decision**: Extract the existing inline logic in `GetParentGroupActivityGalleryQuery` into a
new `IGroupActivityChildDerivationService` (single method, e.g.
`GetDepictedChildIdsAsync(groupActivityId)`), implemented once in Infrastructure, and have the
gallery query, the archival job, and `PurgeChildPhotosCommand` all call it.

**Rationale**: This is the single highest-risk place for a silent correctness bug in this
feature — if the archival job's derivation ever drifts from the gallery query's (e.g. one uses
the activity's group at query-time vs. at activity-time, or a different date-matching rule), a
photo could be archived/purged while a family can still see it in their gallery, or vice versa.
A shared service makes that structurally impossible rather than relying on three call sites
staying manually in sync. This is a refactor of existing 009b code, not new business behavior —
`GetParentGroupActivityGalleryQuery`'s observable output is unchanged.

## R4 — Audit logging: reuse the existing `ILogger` pattern, do not add a new table

**Question**: spec.md's Clarifications commit to an audit trail for every purge action ("who
purged what, when, for which child"). Is there an existing `AuditLog` entity to write to?

**Decision**: No such entity exists anywhere in the codebase (confirmed: 008a's own
`data-model.md` explicitly states "there is no `AuditLog` entity anywhere in this codebase yet,
and adding one is out of scope for this feature" — the same conclusion `CorrectShiftCommand`
reached later, reusing the same mechanism rather than introducing a table). This feature follows
that precedent: `PurgeChildPhotosCommand`'s handler logs a structured `ILogger.LogWarning` (or
`LogInformation`, since a *successful* purge is not itself a security event the way a rejected
device is — but see below) entry with `{TenantId}`, `{ActorUserId}`, `{ActorRole}`, `{ChildId}`,
`{DeletedObjectCount}`, `{FailedObjectCount}`, mirroring the shape of the existing device-
rejection log line (`Program.cs`'s `OnTokenValidated` handler) and `CorrectShiftCommand`'s reuse
of it.

**Rationale**: Introducing the codebase's first persisted audit-log table for a single feature's
one command would be a new architectural pattern adopted without a second consumer to justify it
— exactly the kind of premature abstraction Constitution VII (Monolith-First Simplicity) and the
project's own "no half-finished implementations, no hypothetical future requirements" convention
warn against. If a third feature needs queryable/reportable audit history (e.g. feature 038's
data-retention destruction audit trail, which explicitly wants a report), that is the point to
introduce a real table serving multiple features at once — not here, first, for one command.
This is flagged explicitly rather than silently decided, since it is a genuine architectural
choice: **if the product owner wants purge history to be queryable/reportable in-app rather than
only in log aggregation, that requires revisiting this decision** (noted as a residual risk, not
blocking implementation, since spec.md's own success criteria (SC-002) do not require a
director-facing audit report — only that the action is never silent).

## R5 — Storage-class values and Terraform lifecycle rule shape

**Decision**: Terraform `lifecycle_rule` on `google_storage_bucket.staff_profile_photos`:

```hcl
lifecycle_rule {
  condition {
    age                   = 90
    matches_storage_class = ["STANDARD"]
    matches_prefix        = [
      "staff/", "children/", "group-activities/", "health-records/", "vaccine-records/",
    ]
  }
  action {
    type          = "SetStorageClass"
    storage_class = "NEARLINE"
  }
}
```

**Thumbnail exclusion problem**: `staff/`, `children/`, `health-records/`, and `vaccine-records/`
each hold exactly one object per subject (no thumbnail), so `matches_prefix` alone is sufficient
and safe for those four. `group-activities/` is different — full-resolution objects
(`{photoId}.jpg`) and their thumbnails (`{photoId}-thumb.jpg`) share the same prefix and the same
`.jpg` suffix, and GCS lifecycle conditions are AND-only (no negation), so no combination of
`matches_prefix`/`matches_suffix` can select "full-resolution but not thumbnail" within that
prefix. Renaming the thumbnail suffix to something the native rule could exclude (e.g. a
different extension) is out of scope (no object-key changes, spec.md constraints).

**Resolution**: the Terraform `lifecycle_rule` above covers only `staff/`, `children/`,
`health-records/`, `vaccine-records/`. `group-activities/` full-resolution objects get the same
90-day-no-activity tiering, but evaluated and applied by the same app-level archival job as R2
(which already targets group-activity photos individually and therefore can address the
full-resolution object directly by path, skipping any `-thumb.jpg` sibling) rather than a native
bucket rule. This keeps FR-004 (thumbnails never transition) unconditionally true without any
object-key renaming, at the cost of one photo type's general tiering running on the daily job's
cadence instead of GCS's own background lifecycle scan — acceptable since both are background,
invisible-to-users processes with no latency requirement (FR-011/FR-006).

Archive-on-departure (R2) always targets `COLDLINE`. General no-activity tiering (this section)
always targets `NEARLINE` — per spec.md's Assumptions (Nearline for occasionally-still-relevant
active-child content, Coldline for departed-child content where access is rare).

## R6 — Attachment-disposition download URLs

**Decision**: Add one new method per storage port,
`CreateAttachmentDownloadUrlAsync(objectPath, downloadFileName)`, returning a V4 signed URL with
`responseDisposition: "attachment; filename=\"{downloadFileName}\""` set on the signing request
(the same `UrlSigner`/`SignedUrlOptions` mechanism `CreateDownloadUrlAsync` already uses, just
with the response-disposition option populated) — not a new signing mechanism. Existing
`CreateDownloadUrlAsync` call sites (inline gallery/profile views) are untouched.
