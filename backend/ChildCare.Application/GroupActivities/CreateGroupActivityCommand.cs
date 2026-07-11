using ChildCare.Application.Common;
using ChildCare.Application.RoomShifts;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.GroupActivities;

// GroupId/LocationId/RecordedByDeviceId come from the recording device's own JWT claims
// (endpoint layer resolves them, mirrors RecordChildEventCommand's identical pattern) — never
// client-supplied.
public record CreateGroupActivityCommand(
    Guid? Id,
    Guid GroupId,
    Guid LocationId,
    Guid RecordedByDeviceId,
    GroupActivityType ActivityType,
    string Title,
    string? Description,
    DateTime OccurredAt) : IRequest<GroupActivityResult>;

public class CreateGroupActivityCommandValidator : AbstractValidator<CreateGroupActivityCommand>
{
    public CreateGroupActivityCommandValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.OccurredAt).NotEmpty();
    }
}

public class CreateGroupActivityCommandHandler(
    ITenantDbContext db,
    IShiftAttributionService attribution,
    GroupActivityMapper mapper)
    : IRequestHandler<CreateGroupActivityCommand, GroupActivityResult>
{
    public async Task<GroupActivityResult> Handle(CreateGroupActivityCommand request, CancellationToken cancellationToken)
    {
        // Idempotent create by client-generated id (mirrors RecordChildEventCommand's FR-013a
        // precedent) — a retried request after a timeout whose original request actually
        // succeeded returns the existing record.
        if (request.Id.HasValue)
        {
            var existing = await db.GroupActivities.FirstOrDefaultAsync(a => a.Id == request.Id.Value, cancellationToken);
            if (existing is not null)
                return GroupActivityResult.Success(await mapper.ToResponseAsync(existing, [], cancellationToken));
        }

        // research.md R1: reuses feature 008a/009's IShiftAttributionService rather than
        // building a second recorded-by resolver.
        var recordedBy = await attribution.ResolveRecordedByAsync(
            request.LocationId, request.GroupId, request.OccurredAt, cancellationToken);

        var activity = new GroupActivity
        {
            Id = request.Id ?? Guid.NewGuid(),
            GroupId = request.GroupId,
            LocationId = request.LocationId,
            ActivityType = request.ActivityType,
            Title = request.Title,
            Description = request.Description,
            OccurredAt = request.OccurredAt,
            RecordedBy = recordedBy.ToList(),
            RecordedByDeviceId = request.RecordedByDeviceId,
        };

        db.GroupActivities.Add(activity);
        await db.SaveChangesAsync(cancellationToken);

        return GroupActivityResult.Success(await mapper.ToResponseAsync(activity, [], cancellationToken));
    }
}
