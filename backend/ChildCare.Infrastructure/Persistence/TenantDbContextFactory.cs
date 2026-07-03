using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChildCare.Infrastructure.Persistence;

/// <summary>
/// Design-time only — used by `dotnet ef migrations add --context TenantDbContext`.
/// The schema name here ("tenant_template") only shapes the generated migration's
/// baseline shape; at runtime, TenantProvisioningService supplies each tenant's real
/// schema name (research.md R6).
/// </summary>
public class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CHILDCARE_DESIGN_TIME_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=childcaredb;Username=childcare;Password=childcare;";

        const string schemaName = "tenant_template";

        var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", schemaName));
        optionsBuilder.ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCacheKeyFactory, DynamicSchemaModelCacheKeyFactory>();

        return new TenantDbContext(optionsBuilder.Options, schemaName);
    }
}
