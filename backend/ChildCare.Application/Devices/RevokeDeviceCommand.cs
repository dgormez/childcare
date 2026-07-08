using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Devices;

/// <summary>
/// FR-021: director revokes a lost/stolen tablet. Sets DevicePairing.RevokedAt — checked on
/// *every* request via the DeviceToken scheme's OnTokenValidated event (Program.cs), not only
/// at issuance, so the very next request from that device is rejected regardless of remaining
/// token validity.
/// </summary>
public record RevokeDeviceCommand(Guid DeviceId) : IRequest<RevokeDeviceResult>;

public class RevokeDeviceCommandHandler(ITenantDbContext db) : IRequestHandler<RevokeDeviceCommand, RevokeDeviceResult>
{
    public async Task<RevokeDeviceResult> Handle(RevokeDeviceCommand request, CancellationToken cancellationToken)
    {
        var pairing = await db.DevicePairings.FirstOrDefaultAsync(d => d.Id == request.DeviceId, cancellationToken);
        if (pairing is null)
            return RevokeDeviceResult.Fail(RevokeDeviceFailure.NotFound);

        pairing.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return RevokeDeviceResult.Success();
    }
}
