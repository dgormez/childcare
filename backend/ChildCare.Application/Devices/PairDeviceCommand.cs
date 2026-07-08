using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Devices;

/// <summary>
/// FR-001/FR-002: director, one-time pairing step. Issues a device token scoped to the chosen
/// location/group (token_version = 1) and stores a director-override PIN hash for later
/// room-mode exit (FR-005) — set here, not reused from anywhere else.
/// </summary>
public record PairDeviceCommand(
    Guid LocationId,
    Guid GroupId,
    string DirectorOverridePin,
    Guid PairedByTenantUserId) : IRequest<DeviceResult>;

public class PairDeviceCommandHandler(ITenantDbContext db, ICurrentTenantService currentTenant, IDeviceTokenIssuer tokenIssuer)
    : IRequestHandler<PairDeviceCommand, DeviceResult>
{
    public async Task<DeviceResult> Handle(PairDeviceCommand request, CancellationToken cancellationToken)
    {
        var locationExists = await db.Locations.AnyAsync(l => l.Id == request.LocationId, cancellationToken);
        if (!locationExists)
            return DeviceResult.Fail(DeviceFailure.LocationNotFound);

        var groupExists = await db.Groups.AnyAsync(
            g => g.Id == request.GroupId && g.LocationId == request.LocationId, cancellationToken);
        if (!groupExists)
            return DeviceResult.Fail(DeviceFailure.GroupNotFound);

        var pairing = new DevicePairing
        {
            TenantId = currentTenant.TenantId,
            LocationId = request.LocationId,
            GroupId = request.GroupId,
            DirectorOverridePinHash = BCrypt.Net.BCrypt.HashPassword(request.DirectorOverridePin),
            TokenIssuedAt = DateTime.UtcNow,
            TokenVersion = 1,
            PairedByTenantUserId = request.PairedByTenantUserId,
        };

        db.DevicePairings.Add(pairing);
        await db.SaveChangesAsync(cancellationToken);

        var deviceToken = tokenIssuer.IssueDeviceToken(
            currentTenant.TenantId, pairing.Id, request.LocationId, request.GroupId, pairing.TokenVersion);

        return DeviceResult.Success(new DevicePairingResponse(pairing.Id, deviceToken, pairing.TokenVersion));
    }
}
