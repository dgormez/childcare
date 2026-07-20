using ChildCare.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Locations;

/// <summary>
/// Feature 021 — spec.md FR-016 requires the decision itself to be traceable. Mirrors feature
/// 008b's UpdateLocationCheckInSettingsCommandHandler exactly (plain ILogger entry on change,
/// no dedicated audit-trail subsystem — this codebase has none, per that feature's precedent).
/// </summary>
public class UpdateLocationQrCheckInSettingCommandHandler(
    ITenantDbContext db,
    ILogger<UpdateLocationQrCheckInSettingCommandHandler> logger) : IRequestHandler<UpdateLocationQrCheckInSettingCommand, LocationResult>
{
    public async Task<LocationResult> Handle(UpdateLocationQrCheckInSettingCommand request, CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId, cancellationToken);
        if (location is null)
            return LocationResult.Fail(LocationFailure.NotFound);

        var previousValue = location.QrCheckInEnabled;
        location.QrCheckInEnabled = request.Enabled;
        location.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (previousValue != request.Enabled)
        {
            logger.LogInformation(
                "Director {DirectorId} changed QrCheckInEnabled for location {LocationId} from {PreviousValue} to {NewValue}",
                request.DirectorId, request.LocationId, previousValue, request.Enabled);
        }

        return LocationResult.Success(LocationMapper.ToResponse(location));
    }
}
