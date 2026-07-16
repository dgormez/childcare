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
    public DbSet<PaymentProviderConnection> PaymentProviderConnections => Set<PaymentProviderConnection>();
    public DbSet<Payment> Payments => Set<Payment>();

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
            // Feature 014 — org-wide Belgian company registration number.
            t.Property(x => x.KboNumber).HasMaxLength(20);
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

        // Feature 014a — one row per organisation's connected Mollie account (research.md R3).
        modelBuilder.Entity<PaymentProviderConnection>(pc =>
        {
            pc.ToTable("payment_provider_connections");
            pc.HasKey(x => x.Id);
            pc.HasIndex(x => x.TenantId).IsUnique();
            pc.Property(x => x.Provider).IsRequired().HasMaxLength(20);
            pc.Property(x => x.ProviderAccountId).IsRequired().HasMaxLength(200);
            pc.Property(x => x.ProviderAccountLabel).IsRequired().HasMaxLength(200);
            pc.Property(x => x.EncryptedAccessToken).IsRequired();
            pc.Property(x => x.EncryptedRefreshToken).IsRequired();
            pc.Property(x => x.Status)
              .HasConversion(
                  v => v.ToString().ToLowerInvariant(),
                  v => (PaymentConnectionStatus)Enum.Parse(typeof(PaymentConnectionStatus), v, ignoreCase: true))
              .HasMaxLength(20)
              .IsRequired();
        });

        // Feature 014a — cross-tenant webhook-resolution index (research.md R2). InvoiceId
        // deliberately has no FK: it references a tenant-schema row PublicDbContext cannot
        // statically resolve, same posture as VaccineRecord.VaccineTypeId (013g).
        modelBuilder.Entity<Payment>(p =>
        {
            p.ToTable("payments");
            p.HasKey(x => x.Id);
            p.HasIndex(x => x.PaymentReference).IsUnique();
            p.HasIndex(x => new { x.TenantId, x.InvoiceId, x.Status });
            p.Property(x => x.ProviderPaymentId).HasMaxLength(100);
            p.Property(x => x.Status)
              .HasConversion(
                  v => v.ToString().ToLowerInvariant(),
                  v => (PaymentStatus)Enum.Parse(typeof(PaymentStatus), v, ignoreCase: true))
              .HasMaxLength(20)
              .IsRequired();
        });
    }

    private static VaccineCategory ParseVaccineCategory(string value) =>
        VaccineCategoryExtensions.TryParseWireString(value, out var parsed)
            ? parsed
            : throw new FormatException($"Unknown VaccineCategory: {value}");
}
