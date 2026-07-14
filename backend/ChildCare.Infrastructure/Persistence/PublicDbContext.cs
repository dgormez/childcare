using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Infrastructure.Persistence;

public class PublicDbContext(DbContextOptions<PublicDbContext> options) : DbContext(options), IPublicDbContext
{
    public DbSet<Tenant>     Tenants     => Set<Tenant>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<VaccineType> VaccineTypes => Set<VaccineType>();

    public void Detach(object entity) => Entry(entity).State = EntityState.Detached;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(t =>
        {
            t.ToTable("tenants", tb =>
            {
                tb.HasCheckConstraint("CK_tenants_plan", "\"Plan\" IN ('trial','starter','pro')");
                tb.HasCheckConstraint("CK_tenants_provisioning_status", "\"ProvisioningStatus\" IN ('provisioning','ready','failed')");
            });
            t.HasKey(x => x.Id);
            t.HasIndex(x => x.Slug).IsUnique();
            t.HasIndex(x => x.SchemaName).IsUnique();
            t.HasIndex(x => x.CreatedFromInvitationId).IsUnique();
            t.Property(x => x.Name).IsRequired().HasMaxLength(200);
            t.Property(x => x.Slug).IsRequired().HasMaxLength(200);
            t.Property(x => x.SchemaName).IsRequired().HasMaxLength(63); // Postgres identifier limit
            t.Property(x => x.Plan)
             .HasConversion(
                 v => v.ToString().ToLowerInvariant(),
                 v => (PlanTier)Enum.Parse(typeof(PlanTier), v, ignoreCase: true))
             .HasMaxLength(20)
             .IsRequired();
            t.Property(x => x.ProvisioningStatus)
             .HasConversion(
                 v => v.ToString().ToLowerInvariant(),
                 v => (ProvisioningStatus)Enum.Parse(typeof(ProvisioningStatus), v, ignoreCase: true))
             .HasMaxLength(20)
             .IsRequired();
        });

        modelBuilder.Entity<Invitation>(i =>
        {
            i.ToTable("invitations");
            i.HasKey(x => x.Id);
            i.Property(x => x.Email).IsRequired().HasMaxLength(254);
            i.Property(x => x.TokenHash).IsRequired();
            i.Property(x => x.ExpiresAt).IsRequired();
        });

        modelBuilder.Entity<VaccineType>(v =>
        {
            v.ToTable("vaccine_types");
            v.HasKey(x => x.Id);
            v.Property(x => x.Name).IsRequired().HasMaxLength(200);
            v.Property(x => x.Category)
             .HasConversion(
                 c => c == null ? null : c.Value.ToWireString(),
                 c => c == null ? null : ParseVaccineCategory(c))
             .HasMaxLength(30);
            v.HasIndex(x => new { x.Category, x.SortOrder });
            v.HasIndex(x => x.IsActive);
            // Feature 013h — deactivation audit trail. DeactivatedByUserId is deliberately not
            // configured with HasOne/FK: it references a TenantUser row in an arbitrary tenant's
            // schema, which PublicDbContext cannot statically resolve or FK-enforce (research.md
            // R2, mirrors VaccineRecord.VaccineTypeId's existing cross-schema-reference pattern).
            v.Property(x => x.DeactivatedByEmail).HasMaxLength(254);
        });
    }

    private static VaccineCategory ParseVaccineCategory(string value) =>
        VaccineCategoryExtensions.TryParseWireString(value, out var parsed)
            ? parsed
            : throw new FormatException($"Unknown VaccineCategory: {value}");
}
