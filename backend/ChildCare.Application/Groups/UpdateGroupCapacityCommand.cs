using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Groups;

/// <summary>
/// Feature 018 — lets a director set/change a group's capacity (spec.md FR-001/Assumptions).
/// Discovered mid-implementation: no update path of any kind existed for `Group` before this
/// feature (only `CreateGroupCommand`), so setting the new `Capacity` field needed one. Scoped
/// narrowly to capacity only, not full group editing, since that's the only gap this feature
/// actually needs closed.
/// </summary>
public record UpdateGroupCapacityCommand(Guid GroupId, int? Capacity) : IRequest<GroupResult>;

public class UpdateGroupCapacityCommandValidator : AbstractValidator<UpdateGroupCapacityCommand>
{
    public UpdateGroupCapacityCommandValidator()
    {
        RuleFor(x => x.Capacity)
            .GreaterThan(0).WithMessage("errors.group.capacity_invalid")
            .When(x => x.Capacity is not null);
    }
}

public class UpdateGroupCapacityCommandHandler(ITenantDbContext db) : IRequestHandler<UpdateGroupCapacityCommand, GroupResult>
{
    public async Task<GroupResult> Handle(UpdateGroupCapacityCommand request, CancellationToken cancellationToken)
    {
        var group = await db.Groups.FirstOrDefaultAsync(g => g.Id == request.GroupId, cancellationToken);
        if (group is null)
            return GroupResult.Fail(GroupFailure.NotFound);

        group.Capacity = request.Capacity;
        await db.SaveChangesAsync(cancellationToken);

        return GroupResult.Success(GroupMapper.ToResponse(group));
    }
}
