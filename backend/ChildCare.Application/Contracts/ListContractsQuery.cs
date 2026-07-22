using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contracts;

/// <summary>
/// Feature 024-esignature (User Story 2, FR-018) — an org-wide contracts list, the minimal
/// screen surface `web/app/(app)/contracts/page.tsx` needs to show every contract's derived
/// signing status and offer a send/resend action, without requiring the director to already be
/// on a specific child's page (spec.md Assumptions: "the minimal contract-signing view/actions
/// it actually needs"). No such listing existed before this feature — ListChildContractsQuery
/// (007) is scoped to one child at a time.
/// </summary>
public record ListContractsQuery : IRequest<IReadOnlyList<ContractSummaryResponse>>;

public class ListContractsQueryHandler(ITenantDbContext db) : IRequestHandler<ListContractsQuery, IReadOnlyList<ContractSummaryResponse>>
{
    public async Task<IReadOnlyList<ContractSummaryResponse>> Handle(ListContractsQuery request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;

        var rows = await (
            from c in db.Contracts
            join child in db.Children on c.ChildId equals child.Id
            join location in db.Locations on c.LocationId equals location.Id
            orderby c.CreatedAt descending
            select new { Contract = c, ChildFirstName = child.FirstName, ChildLastName = child.LastName, LocationName = location.Name })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ContractSummaryResponse(
            r.Contract.Id,
            r.Contract.ChildId,
            $"{r.ChildFirstName} {r.ChildLastName}",
            r.LocationName,
            r.Contract.StartDate,
            r.Contract.DailyRateCents,
            r.Contract.Status.ToString().ToLowerInvariant(),
            ContractSigningStatusResolver.Resolve(r.Contract, utcNow).ToString().ToLowerInvariant(),
            r.Contract.SignedAt)).ToList();
    }
}
