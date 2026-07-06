using MediatR;

namespace ChildCare.Application.Locations;

public record UpdateLocationCommand(
    Guid Id,
    string Name,
    string Address,
    string Phone,
    string Email,
    int MaxCapacity,
    string? NaamLocatie,
    string? Dossiernummer,
    string? Verantwoordelijke,
    bool FlexPermission,
    bool BoPermission) : IRequest<LocationResult>;
