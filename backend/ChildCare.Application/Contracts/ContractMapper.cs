using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.Contracts;

internal static class ContractMapper
{
    public static ContractResponse ToResponse(Contract c) => new(
        c.Id,
        c.ChildId,
        c.LocationId,
        c.PreviousContractId,
        c.StartDate,
        c.EndDate,
        c.ContractedDays.Select(d => new ContractedDayResponse(d.Weekday, d.StartTime, d.EndTime)).ToList(),
        c.DailyRateCents,
        c.Status.ToString().ToLowerInvariant(),
        new ContractConsentResponse(
            c.Consent.PhotosInternal,
            c.Consent.PhotosWebsite,
            c.Consent.PhotosSocialMedia,
            c.Consent.VideoInternal,
            c.Consent.PhotosPress),
        ContractSigningStatusResolver.Resolve(c, DateTime.UtcNow).ToString().ToLowerInvariant(),
        c.SignedAt,
        c.SepaIbanLast4 is null ? null : $"•••• {c.SepaIbanLast4}",
        c.SepaMandateReference,
        ResolveMandateStatus(c),
        c.SepaRevokedAt);

    // Feature 026 — none/signed/revoked, precedence order matches data-model.md's eligibility
    // exclusion priority (never-signed vs. signed-then-revoked).
    private static string ResolveMandateStatus(Contract c) =>
        c.SepaAuthorisedAt is null ? "none" : c.SepaRevokedAt is null ? "signed" : "revoked";
}
