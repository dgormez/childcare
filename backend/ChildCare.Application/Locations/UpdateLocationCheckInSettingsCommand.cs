using MediatR;

namespace ChildCare.Application.Locations;

public record UpdateLocationCheckInSettingsCommand(
    Guid LocationId,
    Guid DirectorId,
    bool RequiresCaregiverPin) : IRequest<LocationResult>;
