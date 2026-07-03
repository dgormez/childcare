namespace ChildCare.Application.Common;

/// <summary>
/// Read-only accessor for the current request's resolved tenant identity. Populated by
/// TenantMiddleware once per request; every handler/repository needing "which organisation
/// is this request for" depends on this interface, never the concrete settable class
/// (research.md R2).
/// </summary>
public interface ICurrentTenantService
{
    Guid   TenantId   { get; }
    string SchemaName { get; }
    string TenantSlug { get; }
}
