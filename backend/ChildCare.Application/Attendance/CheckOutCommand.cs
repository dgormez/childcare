using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Attendance;

public record CheckOutCommand(Guid ChildId, Guid LocationId, DateOnly Date) : IRequest<AttendanceResult>;

public class CheckOutCommandValidator : AbstractValidator<CheckOutCommand>
{
    public CheckOutCommandValidator()
    {
        RuleFor(x => x.ChildId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
    }
}

public class CheckOutCommandHandler(ITenantDbContext db) : IRequestHandler<CheckOutCommand, AttendanceResult>
{
    public async Task<AttendanceResult> Handle(CheckOutCommand request, CancellationToken cancellationToken)
    {
        // FR-002a: only a status=present record with CheckInAt set and CheckOutAt still null
        // qualifies — covers both "never checked in" and "already checked out" as not-found,
        // never silently overwriting an existing CheckOutAt.
        var record = await db.AttendanceRecords.FirstOrDefaultAsync(
            r => r.ChildId == request.ChildId && r.LocationId == request.LocationId && r.Date == request.Date
                 && r.Status == AttendanceStatus.Present && r.CheckInAt != null && r.CheckOutAt == null,
            cancellationToken);

        if (record is null)
            return AttendanceResult.Fail(AttendanceFailure.NotFound);

        record.CheckOutAt = DateTime.UtcNow;
        record.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return AttendanceResult.Success(AttendanceMapper.ToResponse(record), created: false);
    }
}
