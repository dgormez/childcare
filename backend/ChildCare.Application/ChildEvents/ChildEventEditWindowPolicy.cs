using ChildCare.Application.Common;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.ChildEvents;

/// <summary>
/// FR-006/FR-007 same-day edit/delete authorization. A director (user JWT) may always edit/
/// delete; a device-token-authenticated tablet may only act on a same-day event recorded at its
/// own paired location. There is no per-caregiver identity to check on the device-token path
/// (constitution: the device token is the tablet's actual security boundary, not individual
/// caregiver auth) — research.md R4 documents this correction from an earlier, unimplementable
/// per-staff-eligibility design.
/// </summary>
public static class ChildEventEditWindowPolicy
{
    /// <param name="isDirector">True when the request carries a director user JWT.</param>
    /// <param name="requestingDeviceLocationId">
    /// The requesting device's own `LocationId` claim; ignored when <paramref name="isDirector"/> is true.
    /// </param>
    public static bool CanModify(ChildEvent childEvent, bool isDirector, Guid? requestingDeviceLocationId)
    {
        if (isDirector)
            return true;

        var isToday = BelgianCalendarDay.ToLocalDate(childEvent.OccurredAt) == BelgianCalendarDay.Today();
        var sameLocation = requestingDeviceLocationId.HasValue && requestingDeviceLocationId.Value == childEvent.LocationId;

        return isToday && sameLocation;
    }
}
