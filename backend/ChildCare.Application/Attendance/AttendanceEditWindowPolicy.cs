using ChildCare.Application.Common;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.Attendance;

/// <summary>
/// FR-010/FR-011: same-day/own-location correction authorization for caregivers, any-day for
/// directors — structurally identical to feature 009's `ChildEventEditWindowPolicy`
/// (research.md R5), since it's the same underlying constraint: a device-token request carries
/// no individual caregiver identity to check eligibility against, only the device's own paired
/// location.
/// </summary>
public static class AttendanceEditWindowPolicy
{
    public static bool CanModify(AttendanceRecord record, bool isDirector, Guid? requestingDeviceLocationId)
    {
        if (isDirector)
            return true;

        var isToday = record.Date == BelgianCalendarDay.Today();
        var sameLocation = requestingDeviceLocationId.HasValue && requestingDeviceLocationId.Value == record.LocationId;

        return isToday && sameLocation;
    }
}
