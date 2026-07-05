namespace ChildCare.Application.Common;

/// <summary>
/// Builds a tenant-scoped context for a given schema. Centralizes the DbContextOptionsBuilder +
/// DynamicSchemaModelCacheKeyFactory wiring so the request pipeline's Scoped registration and
/// every exempt-route auth command (LoginCommandHandler, GoogleSignInCommandHandler, etc. —
/// feature 003, research.md R1, superseding feature 002's deleted AuthService pre-auth shim)
/// both go through one reviewed code path instead of two subtly different copies. Returns
/// ITenantDbContext rather than the concrete TenantDbContext class — see ITenantDbContext's
/// doc comment for why.
/// </summary>
public interface ITenantDbContextResolver
{
    ITenantDbContext ForSchema(string schemaName);
}
