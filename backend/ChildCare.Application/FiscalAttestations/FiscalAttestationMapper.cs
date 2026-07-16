using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;

namespace ChildCare.Application.FiscalAttestations;

internal static class FiscalAttestationMapper
{
    public static FiscalAttestationResponse ToResponse(FiscalAttestation attestation, string childName, string locationName)
    {
        var periods = FiscalAttestationPeriods.FromJson(attestation.Periods);

        return new FiscalAttestationResponse(
            attestation.Id,
            attestation.ChildId,
            childName,
            attestation.LocationId,
            locationName,
            attestation.TaxYear,
            attestation.TotalAmountCents,
            "generated",
            periods.Select(p => new FiscalAttestationPeriodResponse(p.PeriodStart, p.PeriodEnd, p.Days, p.AmountCents, p.DailyRateCents)).ToList(),
            attestation.GeneratedAt);
    }

    // data-model.md's State/lifecycle — an eligible child with no FiscalAttestation row yet is
    // a transient "not yet generated" projection, not stored data.
    public static FiscalAttestationResponse NotYetGenerated(Guid childId, string childName, Guid locationId, string locationName, int taxYear) =>
        new(null, childId, childName, locationId, locationName, taxYear, null, "notYetGenerated", null, null);
}
