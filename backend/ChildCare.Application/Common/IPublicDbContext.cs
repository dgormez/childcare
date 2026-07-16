using ChildCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Common;

/// <summary>
/// Port over the shared public schema (tenants, invitations, and — since feature 013g — the
/// shared vaccine catalog, the first table here that is reference data rather than a
/// tenant-management record, research.md R1). Implemented by ChildCare.Infrastructure's
/// PublicDbContext — Application depends on this abstraction, not the concrete EF Core/Npgsql
/// context, to avoid a circular project reference (Infrastructure already depends on
/// Application for command/handler types).
/// </summary>
public interface IPublicDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<VaccineType> VaccineTypes { get; }
    DbSet<PaymentProviderConnection> PaymentProviderConnections { get; }
    DbSet<Payment> Payments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops tracking an entity — used to discard a failed insert attempt (e.g. after
    /// losing a concurrency race) before re-querying for the authoritative state.</summary>
    void Detach(object entity);
}
