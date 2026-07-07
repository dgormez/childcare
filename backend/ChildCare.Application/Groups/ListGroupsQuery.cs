using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Groups;

public record ListGroupsQuery(Guid? LocationId = null) : IRequest<IReadOnlyList<GroupResponse>>;

public class ListGroupsQueryHandler(ITenantDbContext db) : IRequestHandler<ListGroupsQuery, IReadOnlyList<GroupResponse>>
{
    public async Task<IReadOnlyList<GroupResponse>> Handle(ListGroupsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Groups.AsQueryable();
        if (request.LocationId is Guid locationId)
            query = query.Where(g => g.LocationId == locationId);

        var groups = await query.ToListAsync(cancellationToken);
        return groups.Select(GroupMapper.ToResponse).ToList();
    }
}
