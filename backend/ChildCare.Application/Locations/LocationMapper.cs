using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.Locations;

internal static class LocationMapper
{
    public static LocationResponse ToResponse(Location l) => new(
        l.Id, l.Name, l.Address, l.Phone, l.Email, l.MaxCapacity,
        l.NaamLocatie, l.Dossiernummer, l.Verantwoordelijke, l.FlexPermission, l.BoPermission,
        l.DeactivatedAt, l.CreatedAt, l.UpdatedAt,
        ReservationModeMapper.ToWire(l.ReservationAbsencesMode),
        ReservationModeMapper.ToWire(l.ReservationExtrasMode),
        ReservationModeMapper.ToWire(l.ReservationSwapsMode),
        l.ReservationNoticeHours,
        l.RequiresCaregiverPin);
}
