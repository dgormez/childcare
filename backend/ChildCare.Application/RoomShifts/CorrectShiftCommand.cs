using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.RoomShifts;

/// <summary>
/// FR-023: director-only correction of a shift's recorded times — covers both a forgotten
/// check-out (auto-closed at midnight) and any other recorded mistake. Emits a structured
/// ILogger entry (no new audit table — data-model.md), the same mechanism FR-021 uses for
/// logging rejected actions from a revoked device.
/// </summary>
public record CorrectShiftCommand(
    Guid RoomShiftId, DateTime? CheckedInAt, DateTime? CheckedOutAt, Guid CorrectedByTenantUserId) : IRequest<RoomShiftCorrectionResult>;

public class CorrectShiftCommandHandler(ITenantDbContext db, ILogger<CorrectShiftCommandHandler> logger)
    : IRequestHandler<CorrectShiftCommand, RoomShiftCorrectionResult>
{
    public async Task<RoomShiftCorrectionResult> Handle(CorrectShiftCommand request, CancellationToken cancellationToken)
    {
        var shift = await db.RoomShifts.FirstOrDefaultAsync(s => s.Id == request.RoomShiftId, cancellationToken);
        if (shift is null)
            return RoomShiftCorrectionResult.NotFound();

        var priorCheckedInAt = shift.CheckedInAt;
        var priorCheckedOutAt = shift.CheckedOutAt;
        var priorClosedReason = shift.ClosedReason;

        if (request.CheckedInAt is { } checkedInAt)
            shift.CheckedInAt = checkedInAt;
        if (request.CheckedOutAt is { } checkedOutAt)
            shift.CheckedOutAt = checkedOutAt;

        await db.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "RoomShift {RoomShiftId} for staff {StaffProfileId} corrected by director {CorrectedBy}: " +
            "CheckedInAt {PriorCheckedInAt} -> {NewCheckedInAt}, CheckedOutAt {PriorCheckedOutAt} -> {NewCheckedOutAt}, " +
            "ClosedReason was {PriorClosedReason}",
            shift.Id, shift.StaffProfileId, request.CorrectedByTenantUserId,
            priorCheckedInAt, shift.CheckedInAt, priorCheckedOutAt, shift.CheckedOutAt, priorClosedReason);

        return RoomShiftCorrectionResult.Success(
            new RoomShiftCorrectionResponse(shift.Id, shift.StaffProfileId, shift.CheckedInAt, shift.CheckedOutAt, shift.ClosedReason));
    }
}
