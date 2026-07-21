using ChildCare.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Locations;

/// <summary>
/// Feature 023 — spec.md FR-016-equivalent traceability. Mirrors feature 021's
/// UpdateLocationQrCheckInSettingCommandHandler exactly (plain ILogger entry on change, no
/// dedicated audit-trail subsystem).
/// </summary>
public class UpdateLocationPublicEnrollmentSettingCommandHandler(
    ITenantDbContext db,
    ILogger<UpdateLocationPublicEnrollmentSettingCommandHandler> logger) : IRequestHandler<UpdateLocationPublicEnrollmentSettingCommand, LocationResult>
{
    public async Task<LocationResult> Handle(UpdateLocationPublicEnrollmentSettingCommand request, CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId, cancellationToken);
        if (location is null)
            return LocationResult.Fail(LocationFailure.NotFound);

        var previousValue = location.PublicEnrollmentEnabled;
        location.PublicEnrollmentEnabled = request.Enabled;
        location.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (previousValue != request.Enabled)
        {
            logger.LogInformation(
                "Director {DirectorId} changed PublicEnrollmentEnabled for location {LocationId} from {PreviousValue} to {NewValue}",
                request.DirectorId, request.LocationId, previousValue, request.Enabled);
        }

        return LocationResult.Success(LocationMapper.ToResponse(location));
    }
}
