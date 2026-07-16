using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.FiscalAttestations;

// Feature 015 — spec.md FR-012. Every (child, location) with at least one Paid invoice in the
// tax year, joined against existing FiscalAttestation rows to compute generated/notYetGenerated
// status transiently (data-model.md's State/lifecycle — no stored status column).
public record ListFiscalAttestationsQuery(int TaxYear) : IRequest<IReadOnlyList<FiscalAttestationResponse>>;

public class ListFiscalAttestationsQueryHandler(ITenantDbContext db)
    : IRequestHandler<ListFiscalAttestationsQuery, IReadOnlyList<FiscalAttestationResponse>>
{
    public async Task<IReadOnlyList<FiscalAttestationResponse>> Handle(ListFiscalAttestationsQuery request, CancellationToken cancellationToken)
    {
        var yearStart = new DateOnly(request.TaxYear, 1, 1);
        var yearEnd = new DateOnly(request.TaxYear, 12, 31);

        var eligiblePairs = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Paid && i.PeriodMonth >= yearStart && i.PeriodMonth <= yearEnd)
            .Select(i => new { i.ChildId, i.LocationId })
            .Distinct()
            .ToListAsync(cancellationToken);

        if (eligiblePairs.Count == 0)
            return [];

        var existing = await db.FiscalAttestations
            .Where(fa => fa.TaxYear == request.TaxYear)
            .ToListAsync(cancellationToken);
        var existingByPair = existing.ToDictionary(fa => (fa.ChildId, fa.LocationId));

        var childIds = eligiblePairs.Select(p => p.ChildId).Distinct().ToList();
        var locationIds = eligiblePairs.Select(p => p.LocationId).Distinct().ToList();
        var children = await db.Children.Where(c => childIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);
        var locations = await db.Locations.Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);

        var responses = new List<FiscalAttestationResponse>();
        foreach (var pair in eligiblePairs)
        {
            var childName = $"{children[pair.ChildId].FirstName} {children[pair.ChildId].LastName}";
            var locationName = locations[pair.LocationId].Name;

            responses.Add(existingByPair.TryGetValue((pair.ChildId, pair.LocationId), out var attestation)
                ? FiscalAttestationMapper.ToResponse(attestation, childName, locationName)
                : FiscalAttestationMapper.NotYetGenerated(pair.ChildId, childName, pair.LocationId, locationName, request.TaxYear));
        }

        return responses;
    }
}
