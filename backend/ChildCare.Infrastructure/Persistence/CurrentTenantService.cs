using ChildCare.Application.Common;

namespace ChildCare.Infrastructure.Persistence;

/// <summary>
/// Scoped, settable implementation of ICurrentTenantService. TenantMiddleware resolves this
/// concrete class (to set it once per request); everything downstream depends on the
/// read-only ICurrentTenantService interface instead (research.md R2).
/// </summary>
public class CurrentTenantService : ICurrentTenantService
{
    public Guid   TenantId   { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string TenantSlug { get; set; } = string.Empty;
}
