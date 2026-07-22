using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contracts;

/// <summary>
/// Feature 024-esignature (User Story 2, FR-018, SC-004) — the persisted, immutable signed PDF
/// (FR-010) a director reaches from the contracts screen once a contract shows "signed". Mirrors
/// GetFiscalAttestationDownloadUrlQuery's download-url shape exactly. The object path is
/// deterministic (GcsSignedContractStorage: "signed-contracts/{contractId}.pdf") rather than a
/// stored column, matching that this port only ever writes to one fixed path per contract.
/// </summary>
public record GetSignedContractDownloadUrlQuery(Guid ContractId) : IRequest<SignedContractDownloadUrlResult>;

public record SignedContractDownloadUrlResult(bool Found, string? Url, DateTime? ExpiresAt);

public class GetSignedContractDownloadUrlQueryHandler(ITenantDbContext db, ISignedContractStorage storage)
    : IRequestHandler<GetSignedContractDownloadUrlQuery, SignedContractDownloadUrlResult>
{
    private static readonly TimeSpan UrlDuration = TimeSpan.FromMinutes(15);

    public async Task<SignedContractDownloadUrlResult> Handle(GetSignedContractDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == request.ContractId, cancellationToken);
        if (contract is null || contract.SignedAt is null)
            return new SignedContractDownloadUrlResult(false, null, null);

        var url = await storage.CreateDownloadUrlAsync($"signed-contracts/{contract.Id}.pdf", cancellationToken);
        return new SignedContractDownloadUrlResult(true, url, DateTime.UtcNow.Add(UrlDuration));
    }
}
