using ChildCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Infrastructure.Persistence;

/// <summary>
/// Connects to a single tenant's schema. The schema name is fixed at construction time —
/// this context is used by TenantProvisioningService to create/migrate a brand-new tenant
/// schema (research.md R6); it is not yet wired into the request pipeline (that is
/// feature 002's TenantMiddleware/ICurrentTenantService).
/// </summary>
public class TenantDbContext(DbContextOptions<TenantDbContext> options, string schemaName) : DbContext(options)
{
    public string SchemaName { get; } = schemaName;

    public DbSet<TenantUser> Users => Set<TenantUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<TenantUser>(u =>
        {
            u.ToTable("users");
            u.HasKey(x => x.Id);
            u.HasIndex(x => x.Email).IsUnique();
            u.Property(x => x.Email).IsRequired().HasMaxLength(254);
            u.Property(x => x.PasswordHash).IsRequired();
            u.Property(x => x.Name).IsRequired().HasMaxLength(200);
        });
    }
}
