using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffLeaveRequests;

// FR-010: the director's "Verlofaanvragen" queue, newest first. Status filter optional.
public record ListLeaveRequestsQuery(string? Status) : IRequest<ListStaffLeaveRequestResult>;

public class ListLeaveRequestsQueryHandler(ITenantDbContext db) : IRequestHandler<ListLeaveRequestsQuery, ListStaffLeaveRequestResult>
{
    public async Task<ListStaffLeaveRequestResult> Handle(ListLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        var query = db.StaffLeaveRequests.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<StaffLeaveRequestStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(r => r.Status == status);
        }

        var entries = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return ListStaffLeaveRequestResult.Success(entries.Select(StaffLeaveRequestMapper.ToResponse).ToList());
    }
}
