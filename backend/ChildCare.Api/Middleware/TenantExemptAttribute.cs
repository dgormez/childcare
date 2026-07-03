namespace ChildCare.Api.Middleware;

/// <summary>
/// Marks a route as exempt from TenantMiddleware's tenant resolution (FR-015, research.md R3).
/// Deny-by-default: a route is tenant-scoped unless it explicitly opts out via
/// .RequireTenantExempt(), keeping the exemption decision visible at the route's own
/// registration rather than buried in a separate middleware allow-list.
/// </summary>
public sealed class TenantExemptAttribute;

public static class TenantExemptEndpointExtensions
{
    public static TBuilder RequireTenantExempt<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new TenantExemptAttribute());
        return builder;
    }
}
