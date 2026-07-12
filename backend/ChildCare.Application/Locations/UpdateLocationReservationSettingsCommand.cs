using MediatR;

namespace ChildCare.Application.Locations;

public record UpdateLocationReservationSettingsCommand(
    Guid LocationId,
    string AbsencesMode,
    string ExtrasMode,
    string SwapsMode,
    int NoticeHours,
    bool ConfirmDespitePending) : IRequest<LocationResult>;
