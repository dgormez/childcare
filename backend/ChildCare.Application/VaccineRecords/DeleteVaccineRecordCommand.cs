using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineRecords;

// CallerRole/CallerTenantUserId (031-photo-lifecycle-governance FR-011): staff must be scoped
// to their assigned location(s) — reusing GetChildByIdQuery's StaffLocationEligibility check.
public record DeleteVaccineRecordCommand(Guid ChildId, Guid Id, string? CallerRole = null, Guid? CallerTenantUserId = null)
    : IRequest<VaccineRecordDeleteResult>;

public class DeleteVaccineRecordCommandHandler(ITenantDbContext db) : IRequestHandler<DeleteVaccineRecordCommand, VaccineRecordDeleteResult>
{
    public async Task<VaccineRecordDeleteResult> Handle(DeleteVaccineRecordCommand request, CancellationToken cancellationToken)
    {
        var record = await db.VaccineRecords
            .SingleOrDefaultAsync(v => v.Id == request.Id && v.ChildId == request.ChildId && v.DeletedAt == null, cancellationToken);
        if (record is null)
            return VaccineRecordDeleteResult.Fail(VaccineRecordFailure.NotFound);

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
                return VaccineRecordDeleteResult.Fail(VaccineRecordFailure.NotFound);
        }

        record.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return VaccineRecordDeleteResult.Success();
    }
}

public class VaccineRecordDeleteResult
{
    public VaccineRecordFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static VaccineRecordDeleteResult Success() => new();
    public static VaccineRecordDeleteResult Fail(VaccineRecordFailure failure) => new() { Failure = failure };
}
