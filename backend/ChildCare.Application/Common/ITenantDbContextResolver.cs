namespace ChildCare.Application.Common;

/// <summary>
/// Builds a tenant-scoped context for a given schema. Centralizes the DbContextOptionsBuilder +
/// DynamicSchemaModelCacheKeyFactory wiring so the request pipeline's Scoped registration and
/// AuthService's pre-auth shim (research.md R7) both go through one reviewed code path
/// instead of two subtly different copies (research.md R1). Returns ITenantDbContext rather
/// than the concrete TenantDbContext class — see ITenantDbContext's doc comment for why.
/// </summary>
public interface ITenantDbContextResolver
{
    ITenantDbContext ForSchema(string schemaName);
}
