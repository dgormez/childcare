# Data Model: Staff Management

## Tenant schema (`TenantDbContext` — extended this feature)

### StaffProfile (new)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `TenantUserId` | `Guid` | required, unique FK → `TenantUser.Id` (research.md R1) — one profile per account |
| `FirstName` | `string(100)` | required (FR-001) |
| `LastName` | `string(100)` | required (FR-001) |
| `Phone` | `string(30)` | required (FR-001), permissive international format (mirrors feature 004's phone validation) |
| `QualificationLevel` | `QualificationLevel?` | nullable at schema level; required by the validator when `TenantUser.Role == Staff`, optional when `Role == Director` (FR-003, research.md R7) |
| `ProfilePhotoObjectPath` | `string(500)?` | nullable — GCS object path, never a URL (research.md R3) |
| `DeactivatedAt` | `DateTime?` | `null` = active; non-null = soft-deleted (FR-010); cleared on reactivation |
| `CreatedAt` | `DateTime` | set at creation |
| `UpdatedAt` | `DateTime` | set at creation, updated on every write |

No `OrganisationId`/tenant column (schema is the isolation boundary, matching `TenantUser`/`Location`).

**State machine**: Two states only, driven by `DeactivatedAt`:

```text
Active (DeactivatedAt = null)
   │  deactivate (FR-010, blocked if any IStaffDeactivationGuard
   │  reports active dependents — none registered by this feature, research.md R4)
   ▼
Deactivated (DeactivatedAt = <timestamp>)
   │  reactivate (FR-012 — always permitted)
   ▼
Active (DeactivatedAt = null)
```

No terminal/hard-deleted state exists in this feature — the cycle above is the entity's entire lifecycle. Historical records authored by a deactivated staff member are untouched by this state (FR-010, SC-004) since nothing else references `StaffProfile` by a cascading delete.

**Deactivation side effects** (FR-010, tasks.md T071/T072, resolving `/speckit-checklist` CHK006/CHK016): deactivating a profile whose linked `TenantUser.Role == Staff` (a) blocks that account's next login attempt and (b) removes all of that `TenantUser`'s `TenantUserRefreshToken` rows, mirroring `ResetPasswordCommandHandler`'s existing session-invalidation pattern — an already-issued 15-minute access token is not proactively revoked (no revocation list exists anywhere in this codebase), but no new one can be silently obtained once the refresh tokens are gone. Deactivating a profile whose linked `TenantUser.Role == Director` has **no login-blocking effect at all** — the director's account is untouched; only their appearance in caregiver rosters/BKR counts is affected.

### QualificationLevel (new enum)

```text
QualifiedCaregiver
Auxiliary
StudentVolunteer
```

Fixed to these three values for Phase 1 (spec.md Assumptions) — counts toward BKR (feature 009) for `QualifiedCaregiver`/`Auxiliary` only, never for `StudentVolunteer`.

### StaffInvitation (new)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `StaffProfileId` | `Guid` | required FK → `StaffProfile.Id` (research.md R2) |
| `Email` | `string(254)` | the address the invitation was sent to |
| `TokenHash` | `byte[]` | hashed token, same codec as feature 001's `Invitation` (research.md R2) |
| `ExpiresAt` | `DateTime` | |
| `CreatedAt` | `DateTime` | |

No `UsedAt` column — "used" is derived from whether the linked `TenantUser.PasswordHash` is non-empty (research.md R2, mirrors feature 001's R10). Tenant-scoped (lives in `TenantDbContext`), unlike feature 001's public-schema `Invitation`.

### StaffLocationEligibility (new join entity)

| Field | Type | Notes |
|---|---|---|
| `StaffProfileId` | `Guid` | FK → `StaffProfile.Id`, part of composite PK |
| `LocationId` | `Guid` | FK → `Location.Id` (feature 004), part of composite PK |
| `CreatedAt` | `DateTime` | when this eligibility was assigned |

Composite PK `(StaffProfileId, LocationId)` — no surrogate `Id` needed, no duplicate rows possible for the same pair. No date/schedule fields (FR-004, feature 011's concern, not this feature's).

## New Application-layer ports

### `IStaffDeactivationGuard` (`ChildCare.Application.Common`)

```csharp
public interface IStaffDeactivationGuard
{
    Task<bool> HasActiveDependentsAsync(Guid staffProfileId, ITenantDbContext db, CancellationToken cancellationToken = default);
}
```

Zero implementations registered by this feature (research.md R4) — `DeactivateStaffProfileCommandHandler` resolves `IEnumerable<IStaffDeactivationGuard>`, which is empty until features 009/011 each add their own.

### `IProfilePhotoStorage` (`ChildCare.Application.Common`)

```csharp
public interface IProfilePhotoStorage
{
    Task<(string ObjectPath, string UploadUrl)> CreateUploadUrlAsync(Guid staffProfileId, CancellationToken cancellationToken = default);
    Task<string?> CreateDownloadUrlAsync(string? objectPath, CancellationToken cancellationToken = default);
}
```

Implemented by `GcsProfilePhotoStorage` (`ChildCare.Infrastructure/Storage/`) using `Google.Cloud.Storage.V1`'s V4 `UrlSigner` (research.md R3). `CreateDownloadUrlAsync` returns `null` when `objectPath` is `null` (no photo set) rather than signing a meaningless URL.

## EF Core configuration (`TenantDbContext.OnModelCreating`)

```csharp
modelBuilder.Entity<StaffProfile>(s =>
{
    s.ToTable("staff_profiles");
    s.HasKey(x => x.Id);
    s.HasIndex(x => x.TenantUserId).IsUnique();
    s.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
    s.Property(x => x.LastName).IsRequired().HasMaxLength(100);
    s.Property(x => x.Phone).IsRequired().HasMaxLength(30);
    s.Property(x => x.QualificationLevel).HasConversion<string>();
    s.Property(x => x.ProfilePhotoObjectPath).HasMaxLength(500);
    s.HasIndex(x => x.DeactivatedAt); // supports the default "active only" list filter
    s.HasOne<TenantUser>().WithOne().HasForeignKey<StaffProfile>(x => x.TenantUserId);
});

modelBuilder.Entity<StaffInvitation>(i =>
{
    i.ToTable("staff_invitations");
    i.HasKey(x => x.Id);
    i.HasIndex(x => x.Email);
    i.HasOne<StaffProfile>().WithMany().HasForeignKey(x => x.StaffProfileId);
});

modelBuilder.Entity<StaffLocationEligibility>(e =>
{
    e.ToTable("staff_location_eligibility");
    e.HasKey(x => new { x.StaffProfileId, x.LocationId });
    e.HasOne<StaffProfile>().WithMany().HasForeignKey(x => x.StaffProfileId);
    e.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
});
```

## Non-persisted: request/response DTOs (`ChildCare.Contracts`)

| DTO | Fields |
|---|---|
| `CreateStaffProfileRequest` | `FirstName`, `LastName`, `Email`, `Phone`, `QualificationLevel?`, `Role` (`Staff` or `Director`), `ExistingTenantUserId?` (set only when attaching to an existing Director account, research.md R6) |
| `UpdateStaffProfileRequest` | `FirstName`, `LastName`, `Phone`, `QualificationLevel?` |
| `AcceptStaffInvitationRequest` | `OrganisationSlug`, `Token`, `Password` — `OrganisationSlug` is required because this is an unauthenticated, tenant-exempt route (no JWT to resolve a tenant from); found during implementation, mirrors `ResetPasswordRequest`'s shape |
| `AssignLocationEligibilityRequest` | `LocationId` |
| `StaffResponse` | `Id`, `TenantUserId`, `FirstName`, `LastName`, `Email`, `Phone`, `Role`, `QualificationLevel?`, `PhotoDownloadUrl?` (freshly signed, research.md R3), `EligibleLocationIds`, `DeactivatedAt`, `CreatedAt`, `UpdatedAt` |
| `RequestPhotoUploadUrlResponse` | `UploadUrl`, `ObjectPath` |

`DeactivateStaffProfileRequest`/`ReactivateStaffProfileRequest`/`UnassignLocationEligibilityRequest` have no body — the target id(s) come from the route.
