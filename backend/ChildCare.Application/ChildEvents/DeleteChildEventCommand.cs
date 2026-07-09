using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.ChildEvents;

public record DeleteChildEventCommand(Guid Id, bool IsDirector, Guid? RequestingDeviceLocationId) : IRequest<ChildEventResult>;

public class DeleteChildEventCommandHandler(ITenantDbContext db) : IRequestHandler<DeleteChildEventCommand, ChildEventResult>
{
    public async Task<ChildEventResult> Handle(DeleteChildEventCommand request, CancellationToken cancellationToken)
    {
        var childEvent = await db.ChildEvents.FirstOrDefaultAsync(
            e => e.Id == request.Id && e.DeletedAt == null, cancellationToken);
        if (childEvent is null)
            return ChildEventResult.Fail(ChildEventFailure.NotFound);

        if (!ChildEventEditWindowPolicy.CanModify(childEvent, request.IsDirector, request.RequestingDeviceLocationId))
            return ChildEventResult.Fail(ChildEventFailure.EditWindowExpired);

        // FR-008: soft-delete — retained, excluded from all subsequent reads.
        childEvent.DeletedAt = DateTime.UtcNow;
        childEvent.UpdatedAt = childEvent.DeletedAt.Value;
        await db.SaveChangesAsync(cancellationToken);

        return ChildEventResult.Success(ChildEventMapper.ToResponse(childEvent));
    }
}
