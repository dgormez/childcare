using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.FiscalAttestations;

// Feature 015 — spec.md FR-011/Security considerations: an attestation that doesn't exist or
// doesn't belong to one of the requesting parent's children produces the identical "not found"
// outcome — mirrors GenerateParentInvoicePdfQuery's enumeration-resistance precedent (014).
public record GetParentFiscalAttestationDownloadUrlQuery(Guid TenantUserId, Guid AttestationId) : IRequest<FiscalAttestationDownloadUrlResult>;

public class GetParentFiscalAttestationDownloadUrlQueryHandler(
    ITenantDbContext db, ICurrentParentContactResolver contactResolver, IMediator mediator)
    : IRequestHandler<GetParentFiscalAttestationDownloadUrlQuery, FiscalAttestationDownloadUrlResult>
{
    public async Task<FiscalAttestationDownloadUrlResult> Handle(GetParentFiscalAttestationDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        var contact = await contactResolver.ResolveAsync(request.TenantUserId, cancellationToken);
        if (contact is null)
            return new FiscalAttestationDownloadUrlResult(false, null, null);

        var attestation = await db.FiscalAttestations.FirstOrDefaultAsync(fa => fa.Id == request.AttestationId, cancellationToken);
        if (attestation is null)
            return new FiscalAttestationDownloadUrlResult(false, null, null);

        var isLinked = await db.ChildContacts.AnyAsync(cc => cc.ChildId == attestation.ChildId && cc.ContactId == contact.Id, cancellationToken);
        if (!isLinked)
            return new FiscalAttestationDownloadUrlResult(false, null, null);

        return await mediator.Send(new GetFiscalAttestationDownloadUrlQuery(request.AttestationId), cancellationToken);
    }
}
