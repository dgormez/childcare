using ChildCare.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ChildCare.Infrastructure.Persistence;

/// <summary>
/// Centralizes the DbContextOptionsBuilder + DynamicSchemaModelCacheKeyFactory wiring for
/// building a TenantDbContext against a given schema (research.md R1). Stateless — reads
/// ConnectionStrings:DefaultConnection fresh on every call, so it's safe to register as a
/// Singleton (mirrors TenantProvisioningService's existing registration).
/// </summary>
public class TenantDbContextResolver(IConfiguration configuration) : ITenantDbContextResolver
{
    public ITenantDbContext ForSchema(string schemaName)
    {
        // A blank schema name means the caller hasn't actually resolved a tenant yet (e.g. the
        // Scoped ITenantDbContext registration reading ICurrentTenantService.SchemaName before
        // TenantMiddleware has run). HasDefaultSchema("") doesn't fail — it silently falls back
        // to Postgres's connection-level search_path — so this must throw here, loudly, rather
        // than let a caller build a context pointed at an unintended schema.
        if (string.IsNullOrEmpty(schemaName))
            throw new InvalidOperationException(
                "TenantDbContextResolver.ForSchema was called with no schema name — the caller " +
                "hasn't resolved a tenant yet.");

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schemaName));
        optionsBuilder.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory, DynamicSchemaModelCacheKeyFactory>();

        return new TenantDbContext(optionsBuilder.Options, schemaName);
    }
}
