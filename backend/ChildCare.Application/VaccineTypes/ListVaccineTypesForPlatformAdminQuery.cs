using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineTypes;

// Feature 013h (contracts/platform-admin-vaccine-types-api.md) — every entry, active and
// inactive, with audit fields. Distinct from 013g's ListVaccineTypesQuery (active-only, no audit
// fields), which this feature leaves entirely unchanged (FR-010).
public record ListVaccineTypesForPlatformAdminQuery : IRequest<IReadOnlyList<PlatformAdminVaccineTypeResponse>>;

public class ListVaccineTypesForPlatformAdminQueryHandler(IPublicDbContext publicDb)
    : IRequestHandler<ListVaccineTypesForPlatformAdminQuery, IReadOnlyList<PlatformAdminVaccineTypeResponse>>
{
    public async Task<IReadOnlyList<PlatformAdminVaccineTypeResponse>> Handle(ListVaccineTypesForPlatformAdminQuery request, CancellationToken cancellationToken)
    {
        var types = await publicDb.VaccineTypes
            .AsNoTracking()
            .OrderBy(v => v.Category)
            .ThenBy(v => v.SortOrder)
            .ToListAsync(cancellationToken);

        return types.Select(PlatformAdminVaccineTypeMapper.ToResponse).ToList();
    }
}
