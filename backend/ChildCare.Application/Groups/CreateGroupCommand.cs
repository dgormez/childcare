using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Groups;

public record CreateGroupCommand(string Name, Guid LocationId) : IRequest<GroupResult>;

public class CreateGroupCommandValidator : AbstractValidator<CreateGroupCommand>
{
    public CreateGroupCommandValidator()
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.group.name_required")
            .MaximumLength(100).WithMessage("errors.group.name_too_long");
    }
}

public class CreateGroupCommandHandler(ITenantDbContext db) : IRequestHandler<CreateGroupCommand, GroupResult>
{
    public async Task<GroupResult> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
    {
        // /speckit-checklist CHK003: a group cannot be newly created against an already-
        // deactivated location — reuses feature 004's errors.location.not_found for both
        // "doesn't exist" and "exists but inactive".
        var locationActive = await db.Locations.AnyAsync(
            l => l.Id == request.LocationId && l.DeactivatedAt == null, cancellationToken);
        if (!locationActive)
            return GroupResult.Fail(GroupFailure.LocationNotFound);

        var group = new Group
        {
            Name = request.Name,
            LocationId = request.LocationId,
        };

        db.Groups.Add(group);
        await db.SaveChangesAsync(cancellationToken);

        return GroupResult.Success(GroupMapper.ToResponse(group));
    }
}
