using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineRecords;

// Directors are full-tenant admins (no per-director location scoping exists anywhere in this
// codebase — only StaffLocationEligibility restricts caregivers), so "every location the
// director manages" (spec.md FR-010) is simply every location in the tenant (research.md R4).
public record ListVaccinationsDueSoonQuery(int WithinDays) : IRequest<IReadOnlyList<VaccinationsDueSoonResponse>>;

public class ListVaccinationsDueSoonQueryHandler(ITenantDbContext db)
    : IRequestHandler<ListVaccinationsDueSoonQuery, IReadOnlyList<VaccinationsDueSoonResponse>>
{
    public async Task<IReadOnlyList<VaccinationsDueSoonResponse>> Handle(ListVaccinationsDueSoonQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var threshold = today.AddDays(request.WithinDays);

        // A child with no currently-active group assignment has no location context to show on
        // this dashboard and is excluded (inner join) — the same assumption GetChildByIdQuery
        // makes when resolving a child's current location.
        var dueSoon = await db.VaccineRecords
            .AsNoTracking()
            .Where(v => v.DeletedAt == null && v.NextDueDate != null && v.NextDueDate <= threshold)
            .Join(db.Children, v => v.ChildId, c => c.Id, (v, c) => new { v, c })
            .Join(db.ChildGroupAssignments.Where(a => a.EndDate == null), vc => vc.v.ChildId, a => a.ChildId, (vc, a) => new { vc.v, vc.c, a })
            .Join(db.Groups, vca => vca.a.GroupId, g => g.Id, (vca, g) => new { vca.v, vca.c, g.LocationId })
            .ToListAsync(cancellationToken);

        // One row per child (FR-010/research.md R4): collapse to each child's most urgent
        // (soonest/most-overdue) due date rather than listing every due-soon record separately.
        return dueSoon
            .GroupBy(x => x.c.Id)
            .Select(g => g.OrderBy(x => x.v.NextDueDate).First())
            .OrderBy(x => x.v.NextDueDate)
            .Select(x => new VaccinationsDueSoonResponse(
                x.c.Id,
                $"{x.c.FirstName} {x.c.LastName}",
                x.LocationId,
                x.v.VaccineName,
                x.v.NextDueDate!.Value,
                x.v.NextDueDate!.Value < today))
            .ToList();
    }
}
