using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Children;

// CallerRole/CallerTenantUserId are feature 008 additions (research.md R6, FR-007a) — found
// during implementation: without this, a caregiver could read any child's medical data by id
// even outside their eligible locations, bypassing the list endpoint's scoping entirely. When
// CallerRole == "staff", the child must have a currently-active ChildGroupAssignment at one of
// the caller's eligible locations, or this returns the same NotFound a nonexistent id would —
// indistinguishable from "doesn't exist", matching this codebase's existing cross-tenant
// lookup precedent (never reveal existence via a different error).
public record GetChildByIdQuery(Guid Id, string? CallerRole = null, Guid? CallerTenantUserId = null) : IRequest<ChildResult>;

public class GetChildByIdQueryHandler(ITenantDbContext db, IProfilePhotoStorage photoStorage)
    : IRequestHandler<GetChildByIdQuery, ChildResult>
{
    public async Task<ChildResult> Handle(GetChildByIdQuery request, CancellationToken cancellationToken)
    {
        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (child is null)
            return ChildResult.Fail(ChildFailure.NotFound);

        if (string.Equals(request.CallerRole, "staff", StringComparison.OrdinalIgnoreCase) && request.CallerTenantUserId is Guid tenantUserId)
        {
            var eligibleLocationIds = db.StaffProfiles
                .Where(p => p.TenantUserId == tenantUserId)
                .Join(db.StaffLocationEligibility, p => p.Id, e => e.StaffProfileId, (p, e) => e.LocationId);
            var isInScope = await db.ChildGroupAssignments
                .Where(a => a.ChildId == request.Id && a.EndDate == null)
                .Join(db.Groups, a => a.GroupId, g => g.Id, (a, g) => g.LocationId)
                .AnyAsync(locationId => eligibleLocationIds.Contains(locationId), cancellationToken);
            if (!isInScope)
                return ChildResult.Fail(ChildFailure.NotFound);
        }

        var photoUrl = await photoStorage.CreateDownloadUrlAsync(child.ProfilePhotoObjectPath, cancellationToken);
        return ChildResult.Success(ChildMapper.ToResponse(child, photoUrl));
    }
}
