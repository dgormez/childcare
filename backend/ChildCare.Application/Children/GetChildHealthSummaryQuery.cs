using ChildCare.Application.Common;
using ChildCare.Application.HealthRecords;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Children;

// Feature 013c (research.md R3): reuses GetChildByIdQuery's exact StaffLocationEligibility
// scoping — an ineligible caller gets the same NotFound a nonexistent child would (never
// reveals existence). Read-only: no write path exists on this query's endpoint (FR-014).
public record GetChildHealthSummaryQuery(Guid ChildId, string? CallerRole = null, Guid? CallerTenantUserId = null)
    : IRequest<ChildHealthSummaryResult>;

public class ChildHealthSummaryResult
{
    public ChildHealthSummaryResponse? Response { get; private init; }
    public bool Succeeded => Response is not null;

    public static ChildHealthSummaryResult Success(ChildHealthSummaryResponse response) => new() { Response = response };
    public static ChildHealthSummaryResult NotFound() => new();
}

public class GetChildHealthSummaryQueryHandler(ITenantDbContext db, IHealthAttachmentStorage storage)
    : IRequestHandler<GetChildHealthSummaryQuery, ChildHealthSummaryResult>
{
    public async Task<ChildHealthSummaryResult> Handle(GetChildHealthSummaryQuery request, CancellationToken cancellationToken)
    {
        var childExists = await db.Children.AnyAsync(c => c.Id == request.ChildId, cancellationToken);
        if (!childExists)
            return ChildHealthSummaryResult.NotFound();

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
                return ChildHealthSummaryResult.NotFound();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var threshold = today.AddDays(30);

        var activeHealthRecordEntities = await db.HealthRecords
            .AsNoTracking()
            .Where(r => r.ChildId == request.ChildId && r.DeletedAt == null && (r.ValidUntil == null || r.ValidUntil >= today))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var activeHealthRecords = new List<HealthRecordResponse>(activeHealthRecordEntities.Count);
        foreach (var record in activeHealthRecordEntities)
            activeHealthRecords.Add(await HealthRecordMapper.ToResponseAsync(record, storage, cancellationToken));

        // FR-013: every due-soon/overdue flag for this one child, not collapsed to the most
        // urgent (unlike the cross-child dashboard, ListVaccinationsDueSoonQuery).
        var dueSoonVaccines = await db.VaccineRecords
            .AsNoTracking()
            .Where(v => v.ChildId == request.ChildId && v.DeletedAt == null && v.NextDueDate != null && v.NextDueDate <= threshold)
            .OrderBy(v => v.NextDueDate)
            .Select(v => new ChildHealthSummaryVaccineFlag(v.VaccineName, v.NextDueDate!.Value, v.NextDueDate!.Value < today))
            .ToListAsync(cancellationToken);

        return ChildHealthSummaryResult.Success(new ChildHealthSummaryResponse(request.ChildId, activeHealthRecords, dueSoonVaccines));
    }
}
