using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ChildCare.Infrastructure.Persistence;

/// <summary>
/// Connects to a single tenant's schema. The schema name is fixed at construction time —
/// this context is used by TenantProvisioningService to create/migrate a brand-new tenant
/// schema (research.md R6), and by ITenantDbContextResolver/TenantMiddleware for per-request
/// resolution (feature 002). Implements ITenantDbContext so ChildCare.Application code can
/// depend on the port instead of this concrete Infrastructure type (research.md R1).
/// </summary>
public class TenantDbContext(DbContextOptions<TenantDbContext> options, string schemaName) : DbContext(options), ITenantDbContext
{
    // Matches TenantDbContextFactory/TenantProvisioningService's design-time placeholder — see
    // MigrateAsync's doc comment for why this has to be substituted at runtime.
    private const string PlaceholderSchema = "tenant_template";

    public string SchemaName { get; } = schemaName;

    public DbSet<TenantUser> Users => Set<TenantUser>();

    public DbSet<TenantUserRefreshToken> RefreshTokens => Set<TenantUserRefreshToken>();

    public DbSet<Location> Locations => Set<Location>();

    /// <summary>
    /// Applies any pending migrations to this schema. Deliberately does NOT call the ordinary
    /// Database.MigrateAsync() — discovered during implementation (tasks.md T032/research.md
    /// R8) that it cannot work here: EF Core bakes the design-time placeholder schema
    /// ("tenant_template") into each generated migration's SQL as a literal string, exactly the
    /// constraint TenantProvisioningService's own baseline-script generation already documents
    /// and works around (research.md R6). This method generates the *pending* script (from
    /// whatever this schema's own __EFMigrationsHistory last recorded, to latest) against that
    /// same placeholder, substitutes this schema's real name in, and executes the result
    /// directly — the same technique, extended to cover incremental rollouts, not just a brand
    /// new tenant's baseline.
    /// </summary>
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var applied = await Database.GetAppliedMigrationsAsync(cancellationToken);
        var fromMigration = applied.LastOrDefault();

        var sql = GeneratePendingSql(fromMigration).Replace(PlaceholderSchema, SchemaName);
        if (string.IsNullOrWhiteSpace(sql))
            return;

        if (sql.Contains(PlaceholderSchema, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Pending migration SQL for '{SchemaName}' still references the placeholder schema " +
                $"'{PlaceholderSchema}' after substitution — refusing to run it.");

        await Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    public async Task<bool> HasPendingMigrationsAsync(CancellationToken cancellationToken = default)
        => (await Database.GetPendingMigrationsAsync(cancellationToken)).Any();

    private static string GeneratePendingSql(string? fromMigration)
    {
        // SQL generation never opens a connection, so this connection string only needs to be
        // syntactically valid for the Npgsql provider to configure itself — mirrors
        // TenantProvisioningService.GenerateBaselineSql().
        var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=script-generation-only",
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", PlaceholderSchema));
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, DynamicSchemaModelCacheKeyFactory>();

        using var context = new TenantDbContext(optionsBuilder.Options, PlaceholderSchema);
        var migrator = context.GetService<IMigrator>();
        return migrator.GenerateScript(fromMigration: fromMigration, options: MigrationsSqlGenerationOptions.Default);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<TenantUser>(u =>
        {
            u.ToTable("users", tb =>
            {
                tb.HasCheckConstraint("CK_users_role", "\"Role\" IN ('director','staff','parent')");
            });
            u.HasKey(x => x.Id);
            u.HasIndex(x => x.Email).IsUnique();
            u.Property(x => x.Email).IsRequired().HasMaxLength(254);
            u.Property(x => x.PasswordHash).IsRequired();
            u.Property(x => x.Name).IsRequired().HasMaxLength(200);
            u.Property(x => x.Role)
             .HasConversion(
                 v => v.ToString().ToLowerInvariant(),
                 v => (UserRole)Enum.Parse(typeof(UserRole), v, ignoreCase: true))
             .HasMaxLength(20)
             .IsRequired();
        });

        modelBuilder.Entity<TenantUserRefreshToken>(t =>
        {
            t.ToTable("refresh_tokens");
            t.HasKey(x => x.Id);
            t.HasIndex(x => x.Token).IsUnique();
            t.Property(x => x.Token).IsRequired().HasMaxLength(128);
            t.Property(x => x.ExpiresAt).IsRequired();
            t.HasOne<TenantUser>()
             .WithMany()
             .HasForeignKey(x => x.TenantUserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

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
            l.HasIndex(x => x.DeactivatedAt);
        });
    }
}
