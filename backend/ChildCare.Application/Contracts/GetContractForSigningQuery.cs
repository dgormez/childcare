using ChildCare.Application.Auth;
using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contracts;

/// <summary>
/// Public, tenant-exempt (FR-005/FR-021). Lets the signing page render the contract, or fail
/// closed, before any signature is attempted (research.md R1/R7).
/// </summary>
public record GetContractForSigningQuery(string OrganisationSlug, string Token) : IRequest<GetContractForSigningResult>;

public record GetContractForSigningResult(bool Found, ContractForSigningResponse? Contract);

public class GetContractForSigningQueryHandler(
    OrganisationSlugResolver slugResolver,
    ITenantDbContextResolver tenantResolver,
    IContractSigningTokenService signingTokenService)
    : IRequestHandler<GetContractForSigningQuery, GetContractForSigningResult>
{
    public async Task<GetContractForSigningResult> Handle(GetContractForSigningQuery request, CancellationToken cancellationToken)
    {
        var tenant = await slugResolver.ResolveAsync(request.OrganisationSlug, cancellationToken);
        if (tenant is null)
            return new GetContractForSigningResult(false, null);

        var contractId = signingTokenService.TryParseToken(request.Token);
        if (contractId is null)
            return new GetContractForSigningResult(false, null);

        var db = tenantResolver.ForSchema(tenant.SchemaName);

        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == contractId, cancellationToken);

        // FR-005/FR-012: fails closed for every invalid-token class alike — not found, already
        // signed, expired, or superseded by a resend/revision (token no longer matches the
        // stored SigningToken) — collapsed to the same "not found" result so the caller can't
        // distinguish which failure mode occurred (spec.md's Edge Cases).
        if (contract is null
            || contract.SignedAt is not null
            || contract.SigningToken != request.Token
            || contract.SigningTokenExpiresAt is null
            || contract.SigningTokenExpiresAt <= DateTime.UtcNow)
        {
            return new GetContractForSigningResult(false, null);
        }

        var child = await db.Children.FirstAsync(c => c.Id == contract.ChildId, cancellationToken);
        var location = await db.Locations.FirstAsync(l => l.Id == contract.LocationId, cancellationToken);

        var primaryContactLocale = await db.ChildContacts
            .Where(cc => cc.ChildId == contract.ChildId)
            .OrderByDescending(cc => cc.IsPrimary)
            .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => c.Locale)
            .FirstOrDefaultAsync(cancellationToken);

        var response = new ContractForSigningResponse(
            $"{child.FirstName} {child.LastName}",
            location.Name,
            contract.ContractedDays.Select(d => new ContractedDayResponse(d.Weekday, d.StartTime, d.EndTime)).ToList(),
            contract.DailyRateCents,
            new ContractConsentResponse(
                contract.Consent.PhotosInternal,
                contract.Consent.PhotosWebsite,
                contract.Consent.PhotosSocialMedia,
                contract.Consent.VideoInternal,
                contract.Consent.PhotosPress),
            string.IsNullOrWhiteSpace(primaryContactLocale) ? "nl" : primaryContactLocale);

        return new GetContractForSigningResult(true, response);
    }
}
