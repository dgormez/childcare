using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffLeaveRequests;

// FR-009/FR-015a: the acting staff member (resolved from the JWT, never a client-supplied id)
// submits a planned leave request.
public record CreateLeaveRequestCommand(Guid TenantUserId, string Type, DateOnly DateFrom, DateOnly DateTo, string? Notes)
    : IRequest<StaffLeaveRequestResult>;

public class CreateLeaveRequestCommandValidator : AbstractValidator<CreateLeaveRequestCommand>
{
    public CreateLeaveRequestCommandValidator()
    {
        RuleFor(x => x.Type).Must(v => StaffLeaveRequestMapper.TryParseType(v, out _));
        RuleFor(x => x.DateTo).GreaterThanOrEqualTo(x => x.DateFrom);
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class CreateLeaveRequestCommandHandler(ITenantDbContext db) : IRequestHandler<CreateLeaveRequestCommand, StaffLeaveRequestResult>
{
    public async Task<StaffLeaveRequestResult> Handle(CreateLeaveRequestCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.TenantUserId == request.TenantUserId, cancellationToken);
        if (profile is null)
            return StaffLeaveRequestResult.Fail(StaffLeaveRequestFailure.ProfileNotFound);

        // contracts/staff-app-api.md: the range must not be entirely in the past, mirroring
        // StaffSchedule's past-date convention.
        if (request.DateTo < BelgianCalendarDay.Today())
            return StaffLeaveRequestResult.Fail(StaffLeaveRequestFailure.InvalidDateRange);

        StaffLeaveRequestMapper.TryParseType(request.Type, out var type);

        var entry = new StaffLeaveRequest
        {
            StaffProfileId = profile.Id,
            Type = type,
            DateFrom = request.DateFrom,
            DateTo = request.DateTo,
            Notes = request.Notes,
        };

        db.StaffLeaveRequests.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        return StaffLeaveRequestResult.Success(StaffLeaveRequestMapper.ToResponse(entry));
    }
}
