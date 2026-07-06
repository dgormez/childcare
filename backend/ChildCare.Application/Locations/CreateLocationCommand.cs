using MediatR;

namespace ChildCare.Application.Locations;

public record CreateLocationCommand(
    string Name,
    string Address,
    string Phone,
    string Email,
    int MaxCapacity) : IRequest<LocationResult>;
