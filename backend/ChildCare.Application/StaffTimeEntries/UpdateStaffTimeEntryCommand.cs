using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffTimeEntries;

// FR-006/FR-007/FR-008/FR-009: director correction of clockedOutAt/function/groupId/notes.
// Rejected once locked (FR-006) unless explicitly unlocked first (FR-007). FR-005a's
// configured-function constraint applies to a corrected function too (FR-008). Overlap with
// another entry for the same staff member is a warning, not a block (FR-009).
public record UpdateStaffTimeEntryCommand(Guid Id, DateTime? ClockedOutAt, string? Function, Guid? GroupId, string? Notes)
    : IRequest<StaffTimeEntryResult>;

public class UpdateStaffTimeEntryCommandHandler(ITenantDbContext db) : IRequestHandler<UpdateStaffTimeEntryCommand, StaffTimeEntryResult>
{
    public async Task<StaffTimeEntryResult> Handle(UpdateStaffTimeEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await db.StaffTimeEntries.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (entry is null)
            return StaffTimeEntryResult.Fail(StaffTimeEntryFailure.NotFound);

        var now = DateTime.UtcNow;
        if (entry.IsLocked(now))
            return StaffTimeEntryResult.Fail(StaffTimeEntryFailure.Locked);

        if (request.Function is not null)
        {
            var profile = await db.StaffProfiles.FirstAsync(p => p.Id == entry.StaffProfileId, cancellationToken);
            if (!StaffTimeEntryFunctionExtensions.TryParseWireString(request.Function, out var function)
                || !profile.TimeEntryFunctions.Contains(function))
                return StaffTimeEntryResult.Fail(StaffTimeEntryFailure.FunctionNotConfigured);

            entry.Function = function;
        }

        if (request.ClockedOutAt is DateTime clockedOutAt)
            entry.ClockedOutAt = clockedOutAt;
        if (request.GroupId is Guid groupId)
            entry.GroupId = groupId;
        if (request.Notes is not null)
            entry.Notes = request.Notes;

        entry.UpdatedAt = now;

        // FR-009: warn (don't block) if this correction now overlaps another entry for the same
        // staff member.
        var overlapWarning = await db.StaffTimeEntries.AnyAsync(
            other => other.Id != entry.Id
                && other.StaffProfileId == entry.StaffProfileId
                && other.ClockedInAt < (entry.ClockedOutAt ?? DateTime.MaxValue)
                && (other.ClockedOutAt ?? DateTime.MaxValue) > entry.ClockedInAt,
            cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        return StaffTimeEntryResult.Success(StaffTimeEntryMapper.ToResponse(entry, now), overlapWarning);
    }
}
