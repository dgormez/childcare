using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Groups;

public record AssignChildToGroupCommand(Guid ChildId, Guid GroupId, DateOnly StartDate) : IRequest<ChildGroupAssignmentResult>;

public class AssignChildToGroupCommandValidator : AbstractValidator<AssignChildToGroupCommand>
{
    public AssignChildToGroupCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty().WithMessage("errors.group.group_id_required");
    }
}

public class AssignChildToGroupCommandHandler(ITenantDbContext db) : IRequestHandler<AssignChildToGroupCommand, ChildGroupAssignmentResult>
{
    public async Task<ChildGroupAssignmentResult> Handle(AssignChildToGroupCommand request, CancellationToken cancellationToken)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == request.ChildId, cancellationToken);
        if (child is null)
            return ChildGroupAssignmentResult.Fail(GroupFailure.ChildNotFound);

        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);
        if (group is null)
            return ChildGroupAssignmentResult.Fail(GroupFailure.NotFound);

        var openAssignment = await db.ChildGroupAssignments
            .Where(a => a.ChildId == request.ChildId && a.EndDate == null)
            .FirstOrDefaultAsync(cancellationToken);

        // FR-008a, /speckit-checklist CHK004: assignments must be entered in chronological order.
        if (openAssignment is not null && request.StartDate <= openAssignment.StartDate)
            return ChildGroupAssignmentResult.Fail(GroupFailure.OutOfChronologicalOrder);

        if (openAssignment is not null)
        {
            openAssignment.EndDate = request.StartDate.AddDays(-1);
        }

        var assignment = new ChildGroupAssignment
        {
            ChildId = request.ChildId,
            GroupId = request.GroupId,
            StartDate = request.StartDate,
        };

        db.ChildGroupAssignments.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);

        return ChildGroupAssignmentResult.Success(GroupMapper.ToAssignmentResponse(assignment, group.Name));
    }
}
