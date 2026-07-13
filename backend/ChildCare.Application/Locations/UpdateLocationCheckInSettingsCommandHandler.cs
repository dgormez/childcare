using ChildCare.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Locations;

/// <summary>
/// Feature 008b. Turning this off deliberately trades away identity assurance at check-in/
/// check-out (spec.md FR-003) — FR-016 requires the decision itself to be traceable, hence the
/// structured log entry below. This codebase has no dedicated audit-trail subsystem to extend
/// (verified: no AuditLog table anywhere), so a plain ILogger entry is the right scope, not a
/// new mechanism.
/// </summary>
public class UpdateLocationCheckInSettingsCommandHandler(
    ITenantDbContext db,
    ILogger<UpdateLocationCheckInSettingsCommandHandler> logger) : IRequestHandler<UpdateLocationCheckInSettingsCommand, LocationResult>
{
    public async Task<LocationResult> Handle(UpdateLocationCheckInSettingsCommand request, CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId, cancellationToken);
        if (location is null)
            return LocationResult.Fail(LocationFailure.NotFound);

        var previousValue = location.RequiresCaregiverPin;
        location.RequiresCaregiverPin = request.RequiresCaregiverPin;
        location.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        if (previousValue != request.RequiresCaregiverPin)
        {
            logger.LogInformation(
                "Director {DirectorId} changed RequiresCaregiverPin for location {LocationId} from {PreviousValue} to {NewValue}",
                request.DirectorId, request.LocationId, previousValue, request.RequiresCaregiverPin);
        }

        return LocationResult.Success(LocationMapper.ToResponse(location));
    }
}
