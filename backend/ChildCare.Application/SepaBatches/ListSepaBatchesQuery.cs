using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.SepaBatches;

// Feature 026 — contracts/sepa-direct-debit-api.md, spec.md FR-008.
public record ListSepaBatchesQuery(Guid LocationId) : IRequest<IReadOnlyList<SepaBatchResponse>>;

public class ListSepaBatchesQueryHandler(ITenantDbContext db) : IRequestHandler<ListSepaBatchesQuery, IReadOnlyList<SepaBatchResponse>>
{
    public async Task<IReadOnlyList<SepaBatchResponse>> Handle(ListSepaBatchesQuery request, CancellationToken cancellationToken)
    {
        return await db.SepaBatches
            .Where(b => b.LocationId == request.LocationId)
            .OrderByDescending(b => b.GeneratedAt)
            .Select(b => new SepaBatchResponse(b.Id, b.ExecutionDate, b.GeneratedAt, b.InvoiceCount, b.TotalCents))
            .ToListAsync(cancellationToken);
    }
}
