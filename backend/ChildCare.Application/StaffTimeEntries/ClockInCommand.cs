using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffTimeEntries;

// FR-001: identity resolved server-side from the JWT (TenantUserId), never a client-supplied
// staff id — mirrors feature 027's GetStaffMeQuery/ReportSickCommand precedent (research.md R2).
public record ClockInCommand(Guid TenantUserId, Guid LocationId, Guid? GroupId, string? Function)
    : IRequest<StaffTimeEntryResult>;

public class ClockInCommandHandler(ITenantDbContext db) : IRequestHandler<ClockInCommand, StaffTimeEntryResult>
{
    public async Task<StaffTimeEntryResult> Handle(ClockInCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.TenantUserId == request.TenantUserId, cancellationToken);
        if (profile is null)
            return StaffTimeEntryResult.Fail(StaffTimeEntryFailure.ProfileNotFound);

        // FR-003: at most one open entry per staff member.
        var alreadyOpen = await db.StaffTimeEntries.AnyAsync(
            e => e.StaffProfileId == profile.Id && e.ClockedOutAt == null, cancellationToken);
        if (alreadyOpen)
            return StaffTimeEntryResult.Fail(StaffTimeEntryFailure.AlreadyClockedIn);

        // FR-001a: the acting staff member must be eligible for the target location.
        var eligible = await db.StaffLocationEligibility.AnyAsync(
            e => e.StaffProfileId == profile.Id && e.LocationId == request.LocationId, cancellationToken);
        if (!eligible)
            return StaffTimeEntryResult.Fail(StaffTimeEntryFailure.LocationNotEligible);

        // FR-004a: a supplied group must belong to the supplied location.
        if (request.GroupId is Guid groupId)
        {
            var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);
            if (group is null || group.LocationId != request.LocationId)
                return StaffTimeEntryResult.Fail(StaffTimeEntryFailure.GroupLocationMismatch);
        }

        // FR-005/FR-005a/FR-010: function selection is a server-enforced boundary, not just a
        // client-side picker convenience.
        if (profile.TimeEntryFunctions.Count == 0)
            return StaffTimeEntryResult.Fail(StaffTimeEntryFailure.NoFunctionConfigured);

        StaffTimeEntryFunction function;
        if (request.Function is null)
        {
            if (profile.TimeEntryFunctions.Count > 1)
                return StaffTimeEntryResult.Fail(StaffTimeEntryFailure.FunctionRequired);

            function = profile.TimeEntryFunctions[0];
        }
        else
        {
            if (!StaffTimeEntryFunctionExtensions.TryParseWireString(request.Function, out function)
                || !profile.TimeEntryFunctions.Contains(function))
                return StaffTimeEntryResult.Fail(StaffTimeEntryFailure.FunctionNotConfigured);
        }

        var now = DateTime.UtcNow;
        var entry = new StaffTimeEntry
        {
            StaffProfileId = profile.Id,
            LocationId = request.LocationId,
            GroupId = request.GroupId,
            ClockedInAt = now,
            Function = function,
        };

        db.StaffTimeEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        return StaffTimeEntryResult.Success(StaffTimeEntryMapper.ToResponse(entry, now));
    }
}
