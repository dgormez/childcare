# Data Model: Location Management

## Tenant schema (`TenantDbContext` — extended this feature)

### Location (new)

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | PK |
| `Name` | `string(200)` | required (FR-001) — internal/display name |
| `Address` | `string(500)` | required (FR-001) |
| `Phone` | `string(30)` | required (FR-001), standard international format validation (assumption) |
| `Email` | `string(254)` | required (FR-001), standard email format validation |
| `MaxCapacity` | `int` | required, `CHECK ("MaxCapacity" > 0)` (FR-001, FR-010) |
| `NaamLocatie` | `string(200)?` | nullable — official name registered with Opgroeien (FR-003) |
| `Dossiernummer` | `string(50)?` | nullable — Opgroeien location identifier, filled in later (FR-003/004) |
| `Verantwoordelijke` | `string(200)?` | nullable — responsible person name for Opgroeien XML reports (FR-003/004) |
| `FlexPermission` | `bool` | default `false` (FR-005) |
| `BoPermission` | `bool` | default `false` (FR-005) |
| `DeactivatedAt` | `DateTime?` | `null` = active; non-null = soft-deleted (FR-008); cleared on reactivation (research.md R2) |
| `CreatedAt` | `DateTime` | set at creation |
| `UpdatedAt` | `DateTime` | set at creation, updated on every write (create/update/deactivate/reactivate/duplicate-target) |

No `OrganisationId`/tenant column (research.md R1) — tenant scoping is structural via schema, matching `TenantUser`.

No `SourceLocationId`/`DuplicatedFromId` column — duplication (FR-015) creates a fully independent row with no persisted link back to its source (research.md R5).

No concurrency token column — last-write-wins (FR-017, research.md R3).

**State machine**: Two states only, driven by `DeactivatedAt`:

```text
Active (DeactivatedAt = null)
   │  deactivate (FR-008, blocked if any ILocationDeactivationGuard
   │  reports active dependents — none registered by this feature, research.md R4)
   ▼
Deactivated (DeactivatedAt = <timestamp>)
   │  reactivate (FR-008, clarified — always permitted)
   ▼
Active (DeactivatedAt = null)
```

No terminal/hard-deleted state exists in this feature (FR-009) — the cycle above is the entity's entire lifecycle.

### EF Core configuration (`TenantDbContext.OnModelCreating`)

```csharp
modelBuilder.Entity<Location>(l =>
{
    l.ToTable("locations", tb =>
    {
        tb.HasCheckConstraint("CK_locations_max_capacity", "\"MaxCapacity\" > 0");
    });
    l.HasKey(x => x.Id);
    l.Property(x => x.Name).IsRequired().HasMaxLength(200);
    l.Property(x => x.Address).IsRequired().HasMaxLength(500);
    l.Property(x => x.Phone).IsRequired().HasMaxLength(30);
    l.Property(x => x.Email).IsRequired().HasMaxLength(254);
    l.Property(x => x.NaamLocatie).HasMaxLength(200);
    l.Property(x => x.Dossiernummer).HasMaxLength(50);
    l.Property(x => x.Verantwoordelijke).HasMaxLength(200);
    l.Property(x => x.FlexPermission).IsRequired();
    l.Property(x => x.BoPermission).IsRequired();
    l.HasIndex(x => x.DeactivatedAt); // supports the default "active only" list filter
});
```

## New Application-layer port

### `ILocationDeactivationGuard` (`ChildCare.Application.Common`)

```csharp
public interface ILocationDeactivationGuard
{
    Task<bool> HasActiveDependentsAsync(Guid locationId, ITenantDbContext db, CancellationToken cancellationToken = default);
}
```

Zero implementations registered by this feature (research.md R4) — `DeactivateLocationCommandHandler` resolves `IEnumerable<ILocationDeactivationGuard>`, which is empty until features 005/007 each add their own.

## Non-persisted: request/response DTOs (`ChildCare.Contracts`)

| DTO | Fields |
|---|---|
| `CreateLocationRequest` | `Name`, `Address`, `Phone`, `Email`, `MaxCapacity` (Opgroeien fields omitted — filled in later per FR-004) |
| `UpdateLocationRequest` | `Name`, `Address`, `Phone`, `Email`, `MaxCapacity`, `NaamLocatie?`, `Dossiernummer?`, `Verantwoordelijke?`, `FlexPermission`, `BoPermission` (full replace — single PUT covers both User Story 1 and User Story 2 fields) |
| `LocationResponse` | `Id`, `Name`, `Address`, `Phone`, `Email`, `MaxCapacity`, `NaamLocatie`, `Dossiernummer`, `Verantwoordelijke`, `FlexPermission`, `BoPermission`, `DeactivatedAt`, `CreatedAt`, `UpdatedAt` |

`DuplicateLocationRequest`/`DeactivateLocationRequest`/`ReactivateLocationRequest` have no body — the source/target location id comes from the route (`/api/locations/{id}/duplicate`, etc.).
