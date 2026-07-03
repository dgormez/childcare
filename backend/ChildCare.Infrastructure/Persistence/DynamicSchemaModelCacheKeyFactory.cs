using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Infrastructure.Persistence;

/// <summary>
/// EF Core caches one compiled model per DbContext type by default. TenantDbContext's
/// default schema varies per instance (research.md R6), so the schema name must be part
/// of the cache key — otherwise every tenant after the first would silently reuse the
/// first tenant's compiled model (and therefore its schema name).
/// </summary>
public class DynamicSchemaModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => context is TenantDbContext tenantContext
            ? (context.GetType(), tenantContext.SchemaName, designTime)
            : (object)(context.GetType(), designTime);
}
