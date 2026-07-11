using ChildCare.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.DayReservations;

/// <summary>FR-019: the parent's own request history, across all statuses, newest first.</summary>
public record ListMyDayReservationsQuery(Guid TenantUserId, Guid? ChildId) : MediatR.IRequest<ListDayReservationsResult>;

public class ListMyDayReservationsQueryHandler(ITenantDbContext db, ICurrentParentContactResolver contactResolver)
    : MediatR.IRequestHandler<ListMyDayReservationsQuery, ListDayReservationsResult>
{
    public async Task<ListDayReservationsResult> Handle(ListMyDayReservationsQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return ListDayReservationsResult.Success([]);

        var childIds = await db.ChildContacts
            .Where(cc => cc.ContactId == contact.Id)
            .Select(cc => cc.ChildId)
            .ToListAsync(cancellationToken);

        var reservations = await db.DayReservations
            .AsNoTracking()
            .Where(x => x.RequestedBy == request.TenantUserId
                && childIds.Contains(x.ChildId)
                && (request.ChildId == null || x.ChildId == request.ChildId))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var children = await db.Children
            .Where(c => childIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => $"{c.FirstName} {c.LastName}", cancellationToken);

        var responses = reservations
            .Select(r => DayReservationMapper.ToResponse(r, children.TryGetValue(r.ChildId, out var name) ? name : ""))
            .ToList();

        return ListDayReservationsResult.Success(responses);
    }
}
