using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.FiscalAttestations;

// Feature 015 — spec.md FR-011. Mirrors GetParentInvoicesQuery's exact contact-resolution shape
// (014) — every linked contact sees the same children's attestations, not just the primary one.
public record GetParentFiscalAttestationsQuery(Guid TenantUserId) : IRequest<GetParentFiscalAttestationsResult>;

public class GetParentFiscalAttestationsResult
{
    public bool Authorized { get; private init; }
    public List<FiscalAttestationResponse>? Attestations { get; private init; }

    public static GetParentFiscalAttestationsResult Ok(List<FiscalAttestationResponse> attestations) => new() { Authorized = true, Attestations = attestations };
    public static GetParentFiscalAttestationsResult Forbidden() => new() { Authorized = false };
}

public class GetParentFiscalAttestationsQueryHandler(ITenantDbContext db, ICurrentParentContactResolver contactResolver)
    : IRequestHandler<GetParentFiscalAttestationsQuery, GetParentFiscalAttestationsResult>
{
    public async Task<GetParentFiscalAttestationsResult> Handle(GetParentFiscalAttestationsQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return GetParentFiscalAttestationsResult.Forbidden();

        var childIds = await db.ChildContacts
            .Where(cc => cc.ContactId == contact.Id)
            .Select(cc => cc.ChildId)
            .ToListAsync(cancellationToken);

        if (childIds.Count == 0)
            return GetParentFiscalAttestationsResult.Ok([]);

        var attestations = await db.FiscalAttestations
            .Where(fa => childIds.Contains(fa.ChildId))
            .ToListAsync(cancellationToken);

        if (attestations.Count == 0)
            return GetParentFiscalAttestationsResult.Ok([]);

        var children = await db.Children.Where(c => childIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, cancellationToken);
        var locationIds = attestations.Select(a => a.LocationId).Distinct().ToList();
        var locations = await db.Locations.Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, cancellationToken);

        var responses = attestations
            .Select(a => FiscalAttestationMapper.ToResponse(a, $"{children[a.ChildId].FirstName} {children[a.ChildId].LastName}", locations[a.LocationId].Name))
            .ToList();

        return GetParentFiscalAttestationsResult.Ok(responses);
    }
}
