using ChildCare.Api.Services;
using ChildCare.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Api.Auth;

/// <summary>
/// US6/FR-020 (research.md R3): after a device-authenticated request completes, mints a
/// replacement device token if the validated one has fewer than <see cref="DeviceTokenService.RotateWithinDays"/>
/// days remaining, returned via the <c>X-Device-Token-Refresh</c> response header — the mobile
/// client swaps it into SecureStore on any response carrying that header (apiClient.ts).
/// Applied to the DeviceAuthenticated route group only, so it always runs on a request that has
/// already passed Program.cs's OnTokenValidated revocation check — a revoked device's request
/// never reaches this filter at all, which is what structurally guarantees FR-030 (revocation
/// always beats rotation, never the other way around).
/// </summary>
public class DeviceTokenRotationFilter(DeviceTokenService tokenService, IDeviceTokenIssuer tokenIssuer) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);

        var deviceIdClaim = context.HttpContext.User.FindFirst(DeviceTokenClaims.DeviceId)?.Value;
        if (!Guid.TryParse(deviceIdClaim, out var deviceId))
            return result;

        var db = context.HttpContext.RequestServices.GetRequiredService<ITenantDbContext>();
        var pairing = await db.DevicePairings.FirstOrDefaultAsync(d => d.Id == deviceId);
        if (pairing is null || pairing.RevokedAt is not null)
            return result; // already rejected upstream — defensive only, never expected here

        var expiresAt = pairing.TokenIssuedAt.AddDays(tokenService.TtlDays);
        if (expiresAt - DateTime.UtcNow > TimeSpan.FromDays(tokenService.RotateWithinDays))
            return result;

        pairing.TokenVersion++;
        pairing.TokenIssuedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var newToken = tokenIssuer.IssueDeviceToken(pairing.TenantId, pairing.Id, pairing.LocationId, pairing.GroupId, pairing.TokenVersion);
        context.HttpContext.Response.Headers["X-Device-Token-Refresh"] = newToken;

        return result;
    }
}
