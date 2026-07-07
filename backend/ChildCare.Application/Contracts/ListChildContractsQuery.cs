using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contracts;

public record ListChildContractsQuery(Guid ChildId) : IRequest<ListChildContractsResult>;

public record ListChildContractsResult(bool ChildFound, IReadOnlyList<ContractResponse> Contracts);

public class ListChildContractsQueryHandler(ITenantDbContext db)
    : IRequestHandler<ListChildContractsQuery, ListChildContractsResult>
{
    public async Task<ListChildContractsResult> Handle(ListChildContractsQuery request, CancellationToken cancellationToken)
    {
        var childExists = await db.Children.AnyAsync(c => c.Id == request.ChildId, cancellationToken);
        if (!childExists)
            return new ListChildContractsResult(false, []);

        // FR-017: full history, most-recent-first.
        var contracts = await db.Contracts
            .Where(c => c.ChildId == request.ChildId)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync(cancellationToken);

        return new ListChildContractsResult(true, contracts.Select(ContractMapper.ToResponse).ToList());
    }
}
