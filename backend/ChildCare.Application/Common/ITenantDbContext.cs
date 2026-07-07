using ChildCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Common;

/// <summary>
/// Port over a single tenant's schema (users, refresh tokens). Implemented by
/// ChildCare.Infrastructure's TenantDbContext — Application depends on this abstraction, not
/// the concrete EF Core/Npgsql context, mirroring IPublicDbContext's existing pattern. This is
/// also why ITenantDbContextResolver.ForSchema returns this interface rather than the concrete
/// TenantDbContext type: TenantDbContext lives in ChildCare.Infrastructure, which already
/// depends on ChildCare.Application (for IPublicDbContext/ITenantProvisioningService) — a
/// ChildCare.Application type referencing TenantDbContext directly would create a circular
/// project reference (discovered during implementation; research.md R1 as originally written
/// didn't account for this).
/// </summary>
public interface ITenantDbContext
{
    string SchemaName { get; }

    DbSet<TenantUser> Users { get; }
    DbSet<TenantUserRefreshToken> RefreshTokens { get; }
    DbSet<Location> Locations { get; }
    DbSet<StaffProfile> StaffProfiles { get; }
    DbSet<StaffInvitation> StaffInvitations { get; }
    DbSet<StaffLocationEligibility> StaffLocationEligibility { get; }
    DbSet<Child> Children { get; }
    DbSet<Contact> Contacts { get; }
    DbSet<ChildContact> ChildContacts { get; }
    DbSet<Group> Groups { get; }
    DbSet<ChildGroupAssignment> ChildGroupAssignments { get; }
    DbSet<VaccinationRecord> VaccinationRecords { get; }
    DbSet<Contract> Contracts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Applies any pending migrations to this tenant's schema (research.md R8).</summary>
    Task MigrateAsync(CancellationToken cancellationToken = default);

    /// <summary>True if this schema has migrations that MigrateAsync would apply (research.md R8, migrate-tenants-cli.md).</summary>
    Task<bool> HasPendingMigrationsAsync(CancellationToken cancellationToken = default);
}
