using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Staff;

/// <summary>
/// Self-service lookup for the caller's own staff profile (feature 008) — resolves
/// TenantUserId from the JWT's ClaimTypes.NameIdentifier claim (extracted by the endpoint,
/// not this query), not from a route parameter, so a caregiver can never look up anyone
/// else's profile through this query.
/// </summary>
public record GetStaffMeQuery(Guid TenantUserId) : IRequest<GetStaffMeResult>;

public record GetStaffMeResult(bool Found, StaffMeResponse? Response);

public class GetStaffMeQueryHandler(ITenantDbContext db) : IRequestHandler<GetStaffMeQuery, GetStaffMeResult>
{
    public async Task<GetStaffMeResult> Handle(GetStaffMeQuery request, CancellationToken cancellationToken)
    {
        var profile = await db.StaffProfiles.FirstOrDefaultAsync(p => p.TenantUserId == request.TenantUserId, cancellationToken);
        if (profile is null)
            return new GetStaffMeResult(false, null);

        var user = await db.Users.FirstAsync(u => u.Id == request.TenantUserId, cancellationToken);

        var eligibleLocationIds = await db.StaffLocationEligibility
            .Where(e => e.StaffProfileId == profile.Id)
            .Select(e => e.LocationId)
            .ToListAsync(cancellationToken);

        return new GetStaffMeResult(true, new StaffMeResponse(
            profile.Id, profile.FirstName, profile.LastName, user.Role.ToString().ToLowerInvariant(), eligibleLocationIds));
    }
}
