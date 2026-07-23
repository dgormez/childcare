using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.StaffDocuments;

// FR-014: every staff member with an EmploymentContract-type document whose ValidUntil is
// within 60 days of today, inclusive of already-past dates (Edge Cases — an already-lapsed
// contract is more urgent, not less).
public record GetContractsExpiringQuery : IRequest<IReadOnlyList<ContractExpiringResponse>>;

public class GetContractsExpiringQueryHandler(ITenantDbContext db) : IRequestHandler<GetContractsExpiringQuery, IReadOnlyList<ContractExpiringResponse>>
{
    public async Task<IReadOnlyList<ContractExpiringResponse>> Handle(GetContractsExpiringQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(60);

        var expiring = await db.StaffDocuments
            .Where(d => d.DeletedAt == null
                && d.DocumentType == StaffDocumentType.EmploymentContract
                && d.ValidUntil != null
                && d.ValidUntil <= horizon)
            .OrderBy(d => d.ValidUntil)
            .Join(db.StaffProfiles, d => d.StaffProfileId, p => p.Id, (d, p) => new { d.StaffProfileId, d.ValidUntil, p.FirstName, p.LastName })
            .ToListAsync(cancellationToken);

        return expiring
            .Select(e => new ContractExpiringResponse(
                e.StaffProfileId,
                $"{e.FirstName} {e.LastName}",
                e.ValidUntil!.Value,
                e.ValidUntil!.Value < today))
            .ToList();
    }
}
