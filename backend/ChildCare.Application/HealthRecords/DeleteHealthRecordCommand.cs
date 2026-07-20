using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.HealthRecords;

// CallerRole/CallerTenantUserId (031-photo-lifecycle-governance FR-011): staff must be scoped
// to their assigned location(s) — reusing GetChildByIdQuery's StaffLocationEligibility check.
public record DeleteHealthRecordCommand(Guid ChildId, Guid Id, string? CallerRole = null, Guid? CallerTenantUserId = null)
    : IRequest<HealthRecordDeleteResult>;

public class DeleteHealthRecordCommandHandler(ITenantDbContext db) : IRequestHandler<DeleteHealthRecordCommand, HealthRecordDeleteResult>
{
    public async Task<HealthRecordDeleteResult> Handle(DeleteHealthRecordCommand request, CancellationToken cancellationToken)
    {
        var record = await db.HealthRecords
            .SingleOrDefaultAsync(r => r.Id == request.Id && r.ChildId == request.ChildId && r.DeletedAt == null, cancellationToken);
        if (record is null)
            return HealthRecordDeleteResult.Fail(HealthRecordFailure.NotFound);

        if (string.Equals(request.CallerRole, "staff", StringComparison.OrdinalIgnoreCase) && request.CallerTenantUserId is Guid tenantUserId)
        {
            var eligibleLocationIds = db.StaffProfiles
                .Where(p => p.TenantUserId == tenantUserId)
                .Join(db.StaffLocationEligibility, p => p.Id, e => e.StaffProfileId, (p, e) => e.LocationId);
            var isInScope = await db.ChildGroupAssignments
                .Where(a => a.ChildId == request.ChildId && a.EndDate == null)
                .Join(db.Groups, a => a.GroupId, g => g.Id, (a, g) => g.LocationId)
                .AnyAsync(locationId => eligibleLocationIds.Contains(locationId), cancellationToken);
            if (!isInScope)
                return HealthRecordDeleteResult.Fail(HealthRecordFailure.NotFound);
        }

        record.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return HealthRecordDeleteResult.Success();
    }
}

public class HealthRecordDeleteResult
{
    public HealthRecordFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static HealthRecordDeleteResult Success() => new();
    public static HealthRecordDeleteResult Fail(HealthRecordFailure failure) => new() { Failure = failure };
}
