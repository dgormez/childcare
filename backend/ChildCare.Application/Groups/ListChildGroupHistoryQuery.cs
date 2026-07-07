using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Groups;

public record ListChildGroupHistoryQuery(Guid ChildId) : IRequest<IReadOnlyList<ChildGroupAssignmentResponse>>;

public class ListChildGroupHistoryQueryHandler(ITenantDbContext db)
    : IRequestHandler<ListChildGroupHistoryQuery, IReadOnlyList<ChildGroupAssignmentResponse>>
{
    public async Task<IReadOnlyList<ChildGroupAssignmentResponse>> Handle(ListChildGroupHistoryQuery request, CancellationToken cancellationToken)
    {
        var assignments = await db.ChildGroupAssignments
            .Where(a => a.ChildId == request.ChildId)
            .OrderByDescending(a => a.StartDate)
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
            return [];

        var groupIds = assignments.Select(a => a.GroupId).Distinct().ToList();
        var groupNames = await db.Groups
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);

        return assignments
            .Select(a => GroupMapper.ToAssignmentResponse(a, groupNames.GetValueOrDefault(a.GroupId, string.Empty)))
            .ToList();
    }
}
