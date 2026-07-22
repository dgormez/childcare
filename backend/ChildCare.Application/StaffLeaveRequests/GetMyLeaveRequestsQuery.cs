using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffLeaveRequests;

// FR-012/FR-015: the caller's own leave requests, newest first — identity resolved from the JWT,
// never returns another staff member's rows.
public record GetMyLeaveRequestsQuery(Guid TenantUserId) : IRequest<GetMyLeaveRequestsResult>;

public record GetMyLeaveRequestsResult(bool Found, IReadOnlyList<StaffLeaveRequestResponse> Entries);

public class GetMyLeaveRequestsQueryHandler(ITenantDbContext db) : IRequestHandler<GetMyLeaveRequestsQuery, GetMyLeaveRequestsResult>
{
    public async Task<GetMyLeaveRequestsResult> Handle(GetMyLeaveRequestsQuery request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.TenantUserId == request.TenantUserId, cancellationToken);
        if (profile is null)
            return new GetMyLeaveRequestsResult(false, []);

        var entries = await db.StaffLeaveRequests
            .Where(r => r.StaffProfileId == profile.Id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return new GetMyLeaveRequestsResult(true, entries.Select(StaffLeaveRequestMapper.ToResponse).ToList());
    }
}
