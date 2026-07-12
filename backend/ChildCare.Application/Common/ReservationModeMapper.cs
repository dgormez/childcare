using ChildCare.Domain.Enums;

namespace ChildCare.Application.Common;

/// <summary>
/// Wire-string conversion for <see cref="ReservationRequestMode"/> (feature 013f), mirroring
/// <c>DayReservationMapper</c>'s <c>ToWire</c>/<c>TryParseType</c> convention exactly. Shared
/// between <c>Locations</c> (settings read/write) and <c>DayReservations</c>
/// (<c>ReservationPolicyResolver</c>, enforcement) — both need the same conversion.
/// </summary>
public static class ReservationModeMapper
{
    public static string ToWire(ReservationRequestMode mode) => mode.ToString().ToLowerInvariant();

    public static bool TryParse(string? value, out ReservationRequestMode mode)
    {
        mode = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Trim().ToLowerInvariant() switch
        {
            "disabled" => Assign(ReservationRequestMode.Disabled, out mode),
            "informational" => Assign(ReservationRequestMode.Informational, out mode),
            "approval" => Assign(ReservationRequestMode.Approval, out mode),
            _ => false,
        };
    }

    private static bool Assign(ReservationRequestMode value, out ReservationRequestMode mode)
    {
        mode = value;
        return true;
    }
}
