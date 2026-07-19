# Data Model: Photo Lifecycle & Governance

No new database tables or columns. This feature layers policy over existing entities and GCS
objects; every "entity" below is either an existing row read for eligibility computation, or a
non-persisted contract (interface method / GCS object state).

## Existing entities read (unchanged)

- **`Child`** (`backend/ChildCare.Domain/Entities/Child.cs`): `DeactivatedAt` (nullable
  `DateTime`) is the sole anchor date for a child's own archive-eligibility (profile photo,
  health/vaccine attachments) and for the group-activity derivation (R3). No new field.
- **`ChildGroupAssignment`** (or equivalent membership record): read by the shared
  `IGroupActivityChildDerivationService` to determine which children were members of a given
  `GroupActivity`'s group on its `OccurredAt` date — unchanged read pattern, only the *call site*
  moves from inline query logic into a shared service (R3).
- **`GroupActivityPhoto`**: `ObjectPath`, `ThumbnailObjectPath` — read, never gains new columns.
- **`HealthRecord` / `VaccineRecord` attachment fields**: `AttachmentObjectPath` (exact
  name per existing entity) — read for purge/delete; no new columns.

## New interface contracts (Application layer)

### `IGroupActivityChildDerivationService` (new)

```csharp
public interface IGroupActivityChildDerivationService
{
    Task<IReadOnlyList<Guid>> GetDepictedChildIdsAsync(Guid groupActivityId, CancellationToken ct);
}
```

Single implementation (`GroupActivityChildDerivationService`, Infrastructure), extracted from
`GetParentGroupActivityGalleryQuery`'s existing inline logic (R3) with identical semantics: every
child whose group-membership places them in the activity's group on the activity's date.
Consumers: `GetParentGroupActivityGalleryQuery` (refactored, behavior unchanged),
`EvaluatePhotoArchivalCommand` (new), `PurgeChildPhotosCommand` (new).

### Storage port additions

```csharp
// IProfilePhotoStorage — new
Task DeleteAsync(string objectPath, CancellationToken ct);
Task<Uri> CreateAttachmentDownloadUrlAsync(string objectPath, string downloadFileName, CancellationToken ct);

// IGroupActivityPhotoStorage — new (DeleteAsync already exists)
Task<Uri> CreateAttachmentDownloadUrlAsync(string objectPath, string downloadFileName, CancellationToken ct);

// IHealthAttachmentStorage — new
Task DeleteAsync(string objectPath, CancellationToken ct);
Task<Uri> CreateAttachmentDownloadUrlAsync(string objectPath, string downloadFileName, CancellationToken ct);
```

`DeleteAsync` on the two new ports mirrors `IGroupActivityPhotoStorage`'s existing best-effort
catch/log semantics (a delete failure is logged, not thrown, so a purge cascade can attempt every
object and aggregate failures per FR-016, rather than aborting on the first error).

### `PurgeChildPhotosCommand` (new, Application/Children)

```csharp
public sealed record PurgeChildPhotosCommand(Guid ChildId) : IRequest<PurgePhotosResult>;

public sealed record PurgePhotosResult(
    bool Succeeded,
    IReadOnlyList<string> DeletedObjectPaths,
    IReadOnlyList<string> FailedObjectPaths,
    PurgePhotosFailure? Failure); // e.g. ChildStillActive, NotFound

public enum PurgePhotosFailure { NotFound, ChildStillActive }
```

Handler flow: load `Child`; if `DeactivatedAt is null` → `ChildStillActive` (400, nothing
deleted); else delete profile photo (if set), every health/vaccine attachment, and every
group-activity photo where `IGroupActivityChildDerivationService.GetDepictedChildIdsAsync`
returns exactly `[ChildId]`; aggregate per-object success/failure; log the structured audit entry
(R4) with the aggregate counts; return `PurgePhotosResult` (never silently reports success on a
partial failure, per FR-016).

### `EvaluatePhotoArchivalCommand` (new, `ChildCare.Api/Cli/`)

Not a MediatR command — a CLI job class following `SendPaymentRemindersCommand`'s exact shape
(static `RunAsync(IServiceProvider)`, loop `PublicDbContext.Tenants.Where(Ready)`, resolve each
via `ITenantDbContextResolver.ForSchema`, per-tenant try/catch, exit code 0/1). Per tenant:

1. Find children with `DeactivatedAt <= UtcNow.AddDays(-30)`.
2. For each: if profile photo not already Coldline, transition it; same for each health/vaccine
   attachment.
3. Find group-activity photos where every `GetDepictedChildIdsAsync` result is inactive for ≥30
   days — transition full-resolution object only (never the thumbnail, FR-004), skip if already
   Coldline.
4. Apply the general 90-day-no-activity → Nearline tiering for `group-activities/` full-
   resolution objects only (R5 — the four other prefixes get this via the native Terraform rule
   instead), based on object `TimeCreated` (age proxy, R2/R5's rationale) — skip objects already
   transitioned to Coldline by step 2/3 (Coldline takes precedence; never move Coldline → Nearline).

## GCS object lifecycle (non-persisted state)

| Object class | Standard → Nearline (90d, no activity) | Standard/Nearline → Coldline (30d post-deactivation) |
|---|---|---|
| Profile photo (`staff/`, `children/`) | Native Terraform `lifecycle_rule` (R5) | App job (R2), per-child |
| Health/vaccine attachment | Native Terraform `lifecycle_rule` (R5) | App job (R2), per-child |
| Group-activity full-res photo | App job (R5 exception) | App job (R2), per-derived-child-set |
| Group-activity thumbnail | Never transitions (FR-004) | Never transitions (FR-004) |

## i18n keys (new)

- `parent-mobile/i18n/locales/{en,nl,fr}.json`: `gallery.downloadOriginal`,
  `gallery.downloadFailed` (exact namespace matches the existing `gallery`/`invoices` convention
  noted in spec.md's UX Requirements).
- `web/` i18n (director purge action): `children.purgePhotos.action`,
  `children.purgePhotos.confirmTitle`, `children.purgePhotos.confirmBody`,
  `children.purgePhotos.success`, `children.purgePhotos.partialFailure`,
  `children.purgePhotos.blockedActiveChild`.
