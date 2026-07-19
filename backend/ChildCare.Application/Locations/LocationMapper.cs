using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.Locations;

internal static class LocationMapper
{
    // menuVariantsWithPublishedContent requires a DB query (which enabled variants currently
    // have a published MonthlyMenu), so it can't be computed from the Location entity alone —
    // every command handler except GetLocationByIdQuery passes the default empty list, since a
    // just-created/updated location's menu content genuinely hasn't changed from that command.
    public static LocationResponse ToResponse(Location l, IReadOnlyList<string>? menuVariantsWithPublishedContent = null) => new(
        l.Id, l.Name, l.Address, l.Phone, l.Email, l.MaxCapacity,
        l.NaamLocatie, l.Dossiernummer, l.Verantwoordelijke, l.FlexPermission, l.BoPermission,
        l.DeactivatedAt, l.CreatedAt, l.UpdatedAt,
        ReservationModeMapper.ToWire(l.ReservationAbsencesMode),
        ReservationModeMapper.ToWire(l.ReservationExtrasMode),
        ReservationModeMapper.ToWire(l.ReservationSwapsMode),
        l.ReservationNoticeHours,
        l.RequiresCaregiverPin,
        l.MenuVariantPriorityOrder,
        menuVariantsWithPublishedContent ?? [],
        l.Erkenningsnummer,
        l.BankAccountNumber,
        l.InvoiceDueDays,
        l.PaymentRemindersEnabled,
        l.PaymentReminderDelayDays,
        l.PaymentReminderCadenceDays,
        l.SiblingDiscountPct,
        l.FamilyInvoiceBundlingEnabled);
}
