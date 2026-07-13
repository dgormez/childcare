using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineTypes;

// Reads IPublicDbContext directly (feature 013g, research.md R1) — vaccine_types is shared,
// platform-wide reference data, not tenant-scoped, so this is not a TenantMiddleware bypass.
public record ListVaccineTypesQuery : IRequest<IReadOnlyList<VaccineTypeResponse>>;

public class ListVaccineTypesQueryHandler(IPublicDbContext publicDb)
    : IRequestHandler<ListVaccineTypesQuery, IReadOnlyList<VaccineTypeResponse>>
{
    public async Task<IReadOnlyList<VaccineTypeResponse>> Handle(ListVaccineTypesQuery request, CancellationToken cancellationToken)
    {
        var types = await publicDb.VaccineTypes
            .AsNoTracking()
            .Where(v => v.IsActive)
            .OrderBy(v => v.Category)
            .ThenBy(v => v.SortOrder)
            .ToListAsync(cancellationToken);

        return types.Select(VaccineTypeMapper.ToResponse).ToList();
    }
}
