using ChildCare.Application.Common;
using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Api.Middleware;

/// <summary>
/// Deny-by-default tenant resolution (FR-015). Implements IMiddleware (not the conventional
/// constructor+InvokeAsync pattern) and is registered Singleton — this is what makes it
/// resolvable from the DI container via GetRequiredService&lt;TenantMiddleware&gt;(), which is
/// what lets integration tests reach FailureInjectionHookForTests on the exact instance the
/// pipeline uses (research.md R3). Because the middleware itself is a singleton, InvokeAsync
/// resolves the *scoped* PublicDbContext/CurrentTenantService from context.RequestServices —
/// never via constructor injection, which would incorrectly capture one request's scoped
/// instances for the singleton's whole lifetime.
/// </summary>
public class TenantMiddleware : IMiddleware
{
    /// <summary>
    /// Test-only seam (research.md R3, mirrors TenantProvisioningService's existing pattern):
    /// if set, invoked immediately before the PublicDbContext lookup, so integration tests can
    /// deterministically simulate a lookup failure (FR-008a) without breaking the real DB
    /// connection. Never set outside tests.
    /// </summary>
    public Action? FailureInjectionHookForTests { get; set; }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var endpoint = context.GetEndpoint();

        // No matched endpoint (e.g. a route that doesn't exist) — let the request fall through
        // to the framework's ordinary 404, rather than preempting it with a tenant rejection.
        // FR-015's deny-by-default only applies to routes that actually exist.
        if (endpoint is null || endpoint.Metadata.GetMetadata<TenantExemptAttribute>() is not null)
        {
            await next(context);
            return;
        }

        var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;
        if (string.IsNullOrEmpty(tenantIdClaim))
        {
            await WriteRejectionAsync(context, StatusCodes.Status401Unauthorized, "errors.tenant.missing");
            return;
        }

        // spec.md Edge Cases: a garbled/malformed (non-GUID) claim is treated the same as an
        // unknown organisation (FR-007), not the same as a missing claim (FR-006).
        if (!Guid.TryParse(tenantIdClaim, out var tenantId))
        {
            await WriteRejectionAsync(context, StatusCodes.Status403Forbidden, "errors.tenant.not_found");
            return;
        }

        var publicDb = context.RequestServices.GetRequiredService<PublicDbContext>();

        Domain.Entities.Tenant? tenant;
        try
        {
            FailureInjectionHookForTests?.Invoke();
            tenant = await publicDb.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        }
        catch (Exception ex)
        {
            // FR-008a: a lookup failure must be indistinguishable to the caller from an unknown
            // tenant — the real exception is logged server-side only.
            var logger = context.RequestServices.GetRequiredService<ILogger<TenantMiddleware>>();
            logger.LogError(ex, "Tenant lookup failed for tenant_id {TenantId}", tenantId);
            await WriteRejectionAsync(context, StatusCodes.Status403Forbidden, "errors.tenant.not_found");
            return;
        }

        if (tenant is null)
        {
            await WriteRejectionAsync(context, StatusCodes.Status403Forbidden, "errors.tenant.not_found");
            return;
        }

        if (tenant.ProvisioningStatus != Domain.Enums.ProvisioningStatus.Ready)
        {
            await WriteRejectionAsync(context, StatusCodes.Status403Forbidden, "errors.tenant.not_ready");
            return;
        }

        var currentTenant = context.RequestServices.GetRequiredService<CurrentTenantService>();
        currentTenant.TenantId   = tenant.Id;
        currentTenant.SchemaName = tenant.SchemaName;
        currentTenant.TenantSlug = tenant.Slug;

        await next(context);
    }

    private static Task WriteRejectionAsync(HttpContext context, int statusCode, string errorKey)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new { errorKey });
    }
}
