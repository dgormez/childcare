using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.RoomShifts;

/// <summary>
/// FR-013/research.md R7: every caregiver eligible at the device's own location, as a
/// photo card, with current checked-in state — powers the room home screen. Scoped to
/// location, not group, since there's no staff-to-group assignment concept in this codebase
/// (research.md R7 — that's feature 011 territory).
/// </summary>
public record GetRoomRosterQuery(Guid LocationId) : IRequest<RoomRosterResponse>;

public class GetRoomRosterQueryHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage, CloseStaleShiftsHelper closeStaleShifts)
    : IRequestHandler<GetRoomRosterQuery, RoomRosterResponse>
{
    public async Task<RoomRosterResponse> Handle(GetRoomRosterQuery request, CancellationToken cancellationToken)
    {
        await closeStaleShifts.CloseStaleShiftsAsync(request.LocationId, DateTime.UtcNow, cancellationToken);

        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId, cancellationToken);

        var eligibleStaffIds = await db.StaffLocationEligibility
            .Where(e => e.LocationId == request.LocationId)
            .Select(e => e.StaffProfileId)
            .ToListAsync(cancellationToken);

        var profiles = await db.StaffProfiles
            .Where(p => eligibleStaffIds.Contains(p.Id) && p.DeactivatedAt == null)
            .OrderBy(p => p.FirstName)
            .ToListAsync(cancellationToken);

        var openShifts = await db.RoomShifts
            .Where(s => s.LocationId == request.LocationId && s.CheckedOutAt == null)
            .ToDictionaryAsync(s => s.StaffProfileId, cancellationToken);

        var cards = new List<RoomRosterCardResponse>();
        foreach (var profile in profiles)
        {
            var photoUrl = await photoStorage.CreateDownloadUrlAsync(profile.ProfilePhotoObjectPath, cancellationToken);
            var checkedIn = openShifts.TryGetValue(profile.Id, out var shift);
            cards.Add(new RoomRosterCardResponse(profile.Id, profile.FirstName, photoUrl, checkedIn, checkedIn ? shift!.CheckedInAt : null));
        }

        return new RoomRosterResponse(location?.RequiresCaregiverPin ?? true, location?.QrCheckInEnabled ?? false, cards);
    }
}
