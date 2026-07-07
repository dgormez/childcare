# Data Model: Child File Management

## Tenant schema (`TenantDbContext` — extended this feature)

### Child (new)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `FirstName` | `string(100)` | required (FR-001) |
| `LastName` | `string(100)` | required (FR-001) |
| `DateOfBirth` | `DateOnly` | required (FR-001) |
| `ProfilePhotoObjectPath` | `string(500)?` | nullable — GCS object path via `IProfilePhotoStorage`, never a URL (research.md R1) |
| `Gender` | `Gender?` | nullable enum (FR-001, optional) |
| `Nationality` | `string(100)?` | nullable free text (FR-001, optional) |
| `AllergiesDescription` | `string(2000)?` | nullable free text (FR-003) |
| `AllergySeverity` | `AllergySeverity?` | nullable enum — only meaningful alongside `AllergiesDescription` (FR-003, research.md R5) |
| `MedicalConditions` | `string(2000)?` | nullable free text (FR-003) |
| `DietaryRestrictions` | `string(2000)?` | nullable free text (FR-003) |
| `GpName` | `string(200)?` | nullable (FR-003) |
| `GpPhone` | `string(30)?` | nullable (FR-003) |
| `HealthInsuranceNumber` | `string(50)?` | nullable (FR-003) |
| `Kindcode` | `string(20)?` | nullable, format `YYMMDD-NNN` (FR-009) — not validated for format in Phase 1, just stored |
| `DeactivatedAt` | `DateTime?` | `null` = active; non-null = soft-deleted (FR-012); cleared on reactivation |
| `CreatedAt` | `DateTime` | set at creation |
| `UpdatedAt` | `DateTime` | set at creation, updated on every write |

No `OrganisationId`/tenant column, no contract FK — a `Child` exists and is fully valid with zero contacts, zero group assignments, and zero contract (FR-002).

**State machine**: Same two-state shape as `Location`/`StaffProfile` — `Active (DeactivatedAt = null)` ⇄ `Deactivated (DeactivatedAt = <timestamp>)`, gated on deactivate by `IChildDeactivationGuard` (empty until feature 007 registers one, research.md R4), always permitted on reactivate.

### Gender / AllergySeverity (new enums)

```text
Gender:          Male, Female, Other
AllergySeverity:  Mild, Moderate, Severe
```

### Contact (new)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `FirstName` | `string(100)` | required |
| `LastName` | `string(100)` | required |
| `Phone` | `string(30)` | required, permissive international format (mirrors feature 004/005's phone convention) |
| `Email` | `string(254)?` | nullable, valid email format when present |
| `Locale` | `string(5)` | required, one of `nl`/`fr`/`en` — used when this contact is a child's primary contact (FR-007) |
| `CreatedAt` | `DateTime` | |
| `UpdatedAt` | `DateTime` | |

Standalone entity — no `TenantUserId`/account link (spec.md Assumptions: contacts are data records only in this feature). Never deleted; a contact simply accumulates/loses `ChildContact` links over time.

### ChildContact (new join entity)

| Field | Type | Notes |
|---|---|---|
| `ChildId` | `Guid` | FK → `Child.Id`, part of composite PK |
| `ContactId` | `Guid` | FK → `Contact.Id`, part of composite PK |
| `Relationship` | `ContactRelationship` | enum: `Mother`, `Father`, `Guardian`, `EmergencyContact`, `AuthorisedPickup` (FR-005) — mutable via update, one value per `(ChildId, ContactId)` pair (research.md R3, revised during `/speckit-analyze` — a route keyed only on `childId`/`contactId` would otherwise be ambiguous if a pair could have more than one row) |
| `CanPickup` | `bool` | (FR-005) |
| `IsPrimary` | `bool` | at most one `true` per `ChildId`, enforced in the Application layer (FR-007) — the first link ever created for a child defaults to `true` |
| `CreatedAt` | `DateTime` | |

Composite PK `(ChildId, ContactId)` — no surrogate `Id` needed, no duplicate rows possible for the same pair (mirrors `StaffLocationEligibility`'s shape, feature 005). Rows are never hard-deleted when a contact is unlinked in the everyday sense of "no longer relevant" — see Assumptions; this feature's only removal path is `UnlinkContactFromChildCommand`, which does delete the specific `ChildContact` row (not the `Contact` itself), since a link (unlike a `Child`/`Contact`/`StaffProfile`) carries no independent history worth preserving once undone.

### Group (new)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `LocationId` | `Guid` | FK → `Location.Id` (feature 004) |
| `Name` | `string(100)` | required |
| `CreatedAt` | `DateTime` | |

Minimal — no capacity, no BKR configuration (spec.md Assumptions, research.md R2). No deactivation state in Phase 1; out of scope alongside full group administration.

### ChildGroupAssignment (new)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `ChildId` | `Guid` | FK → `Child.Id` |
| `GroupId` | `Guid` | FK → `Group.Id` |
| `StartDate` | `DateOnly` | required (FR-008a) |
| `EndDate` | `DateOnly?` | nullable — null = current/open-ended assignment; set automatically when a newer assignment starts (FR-008a) |
| `CreatedAt` | `DateTime` | |

Full history retained — assigning a new group never deletes or overwrites a prior `ChildGroupAssignment` row, only sets its `EndDate`.

### VaccinationRecord (new)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `ChildId` | `Guid` | FK → `Child.Id` |
| `VaccineName` | `string(200)` | required (FR-010) |
| `DateAdministered` | `DateOnly` | required (FR-010) |
| `NextDueDate` | `DateOnly?` | nullable — when present and `<= today`, the record is flagged due (FR-011); computed at query time, not a stored flag |
| `CreatedAt` | `DateTime` | |

## Modified Application-layer port

### `IProfilePhotoStorage` (`ChildCare.Application.Common`) — generalized (research.md R1)

```csharp
public interface IProfilePhotoStorage
{
    Task<(string ObjectPath, string UploadUrl)> CreateUploadUrlAsync(string category, Guid subjectId, CancellationToken cancellationToken = default);
    Task<string?> CreateDownloadUrlAsync(string? objectPath, CancellationToken cancellationToken = default);
}
```

`GcsProfilePhotoStorage`'s object path becomes `{category}/{subjectId}/photo.jpg` (was hardcoded `staff/{staffProfileId}/photo.jpg`). Feature 005's call sites now pass `"staff"` explicitly; this feature's `RequestChildPhotoUploadUrlCommandHandler` passes `"children"`.

### `IChildDeactivationGuard` (`ChildCare.Application.Common`, new)

```csharp
public interface IChildDeactivationGuard
{
    Task<bool> HasActiveDependentsAsync(Guid childId, ITenantDbContext db, CancellationToken cancellationToken = default);
}
```

Zero implementations registered by this feature (research.md R4) — `DeactivateChildCommandHandler` resolves `IEnumerable<IChildDeactivationGuard>`, empty until feature 007 adds one.

## EF Core configuration (`TenantDbContext.OnModelCreating`, additions)

```csharp
modelBuilder.Entity<Child>(c =>
{
    c.ToTable("children");
    c.HasKey(x => x.Id);
    c.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
    c.Property(x => x.LastName).IsRequired().HasMaxLength(100);
    c.Property(x => x.DateOfBirth).IsRequired();
    c.Property(x => x.ProfilePhotoObjectPath).HasMaxLength(500);
    c.Property(x => x.Nationality).HasMaxLength(100);
    c.Property(x => x.AllergiesDescription).HasMaxLength(2000);
    c.Property(x => x.MedicalConditions).HasMaxLength(2000);
    c.Property(x => x.DietaryRestrictions).HasMaxLength(2000);
    c.Property(x => x.GpName).HasMaxLength(200);
    c.Property(x => x.GpPhone).HasMaxLength(30);
    c.Property(x => x.HealthInsuranceNumber).HasMaxLength(50);
    c.Property(x => x.Kindcode).HasMaxLength(20);
    c.HasIndex(x => x.DeactivatedAt);
});

modelBuilder.Entity<Contact>(c =>
{
    c.ToTable("contacts");
    c.HasKey(x => x.Id);
    c.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
    c.Property(x => x.LastName).IsRequired().HasMaxLength(100);
    c.Property(x => x.Phone).IsRequired().HasMaxLength(30);
    c.Property(x => x.Email).HasMaxLength(254);
    c.Property(x => x.Locale).IsRequired().HasMaxLength(5);
});

modelBuilder.Entity<ChildContact>(cc =>
{
    cc.ToTable("child_contacts");
    cc.HasKey(x => new { x.ChildId, x.ContactId });
    cc.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
    cc.HasOne<Contact>().WithMany().HasForeignKey(x => x.ContactId);
});

modelBuilder.Entity<Group>(g =>
{
    g.ToTable("groups");
    g.HasKey(x => x.Id);
    g.Property(x => x.Name).IsRequired().HasMaxLength(100);
    g.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId);
});

modelBuilder.Entity<ChildGroupAssignment>(a =>
{
    a.ToTable("child_group_assignments");
    a.HasKey(x => x.Id);
    a.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
    a.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId);
    a.HasIndex(x => new { x.ChildId, x.EndDate });
});

modelBuilder.Entity<VaccinationRecord>(v =>
{
    v.ToTable("vaccination_records");
    v.HasKey(x => x.Id);
    v.Property(x => x.VaccineName).IsRequired().HasMaxLength(200);
    v.HasOne<Child>().WithMany().HasForeignKey(x => x.ChildId);
});
```

## Non-persisted: request/response DTOs (`ChildCare.Contracts`)

| DTO | Fields |
|---|---|
| `CreateChildRequest` | `FirstName`, `LastName`, `DateOfBirth`, `Gender?`, `Nationality?`, `AllergiesDescription?`, `AllergySeverity?`, `MedicalConditions?`, `DietaryRestrictions?`, `GpName?`, `GpPhone?`, `HealthInsuranceNumber?`, `Kindcode?` |
| `UpdateChildRequest` | Same fields as create (all optional ones stay optional) |
| `ChildResponse` | `Id`, `FirstName`, `LastName`, `DateOfBirth`, `PhotoDownloadUrl?`, `Gender?`, `Nationality?`, medical fields, `Kindcode?`, `DeactivatedAt`, `CreatedAt`, `UpdatedAt` |
| `CreateContactRequest` / `UpdateContactRequest` | `FirstName`, `LastName`, `Phone`, `Email?`, `Locale` |
| `ContactResponse` | `Id`, `FirstName`, `LastName`, `Phone`, `Email?`, `Locale` |
| `LinkContactToChildRequest` | `ContactId`, `Relationship`, `CanPickup`, `IsPrimary` |
| `ChildContactResponse` | `ContactId`, `Relationship`, `CanPickup`, `IsPrimary`, plus the linked `ContactResponse` fields inline |
| `CreateGroupRequest` | `Name`, `LocationId` |
| `GroupResponse` | `Id`, `Name`, `LocationId` |
| `AssignChildToGroupRequest` | `GroupId`, `StartDate` |
| `ChildGroupAssignmentResponse` | `GroupId`, `GroupName`, `StartDate`, `EndDate?` |
| `RecordVaccinationRequest` | `VaccineName`, `DateAdministered`, `NextDueDate?` |
| `VaccinationResponse` | `Id`, `VaccineName`, `DateAdministered`, `NextDueDate?`, `IsDue` (computed) |

`DeactivateChildRequest`/`ReactivateChildRequest`/`UnlinkContactFromChildRequest` have no body — target id(s) come from the route.
