using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineCustomEntries;

// Tenant-scoped by construction (ITenantDbContext, resolved per-request via TenantMiddleware) —
// never returns another tenant's remembered entries (spec.md FR-008).
public record ListTenantCustomVaccineEntriesQuery : IRequest<IReadOnlyList<CustomVaccineEntryResponse>>;

public class ListTenantCustomVaccineEntriesQueryHandler(ITenantDbContext db)
    : IRequestHandler<ListTenantCustomVaccineEntriesQuery, IReadOnlyList<CustomVaccineEntryResponse>>
{
    public async Task<IReadOnlyList<CustomVaccineEntryResponse>> Handle(ListTenantCustomVaccineEntriesQuery request, CancellationToken cancellationToken)
    {
        var entries = await db.TenantCustomVaccineEntries
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .ToListAsync(cancellationToken);

        return entries.Select(e => new CustomVaccineEntryResponse(e.Id, e.Name)).ToList();
    }
}
