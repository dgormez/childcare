using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.FiscalAttestations;

// Feature 015 — contracts/fiscal-attestations-api.md's director download-url route.
public record GetFiscalAttestationDownloadUrlQuery(Guid AttestationId) : IRequest<FiscalAttestationDownloadUrlResult>;

public record FiscalAttestationDownloadUrlResult(bool Found, string? Url, DateTime? ExpiresAt);

public class GetFiscalAttestationDownloadUrlQueryHandler(ITenantDbContext db, IFiscalAttestationStorage storage)
    : IRequestHandler<GetFiscalAttestationDownloadUrlQuery, FiscalAttestationDownloadUrlResult>
{
    private static readonly TimeSpan UrlDuration = TimeSpan.FromMinutes(15);

    public async Task<FiscalAttestationDownloadUrlResult> Handle(GetFiscalAttestationDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        var attestation = await db.FiscalAttestations.FirstOrDefaultAsync(fa => fa.Id == request.AttestationId, cancellationToken);
        if (attestation is null)
            return new FiscalAttestationDownloadUrlResult(false, null, null);

        var url = await storage.CreateDownloadUrlAsync(attestation.PdfObjectPath, cancellationToken);
        return new FiscalAttestationDownloadUrlResult(true, url, DateTime.UtcNow.Add(UrlDuration));
    }
}
