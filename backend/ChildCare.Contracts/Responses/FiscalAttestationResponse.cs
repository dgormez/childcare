namespace ChildCare.Contracts.Responses;

// Feature 015 — contracts/fiscal-attestations-api.md. Id/GeneratedAt/Periods are null when
// Status is "notYetGenerated" — a transient row computed by joining eligible children against
// existing FiscalAttestation rows (data-model.md's State/lifecycle section), not a stored state.
public record FiscalAttestationResponse(
    Guid? Id,
    Guid ChildId,
    string ChildName,
    Guid LocationId,
    string LocationName,
    int TaxYear,
    int? TotalAmountCents,
    string Status,
    IReadOnlyList<FiscalAttestationPeriodResponse>? Periods,
    DateTime? GeneratedAt);

public record FiscalAttestationPeriodResponse(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int Days,
    int AmountCents,
    int? DailyRateCents);

// contracts/fiscal-attestations-api.md — POST /api/fiscal-attestations/generate response.
public record GenerateFiscalAttestationsResponse(int TaxYear, IReadOnlyList<GenerateFiscalAttestationsResultItem> Results);

public record GenerateFiscalAttestationsResultItem(Guid ChildId, Guid LocationId, string Status);

public record FiscalAttestationDownloadUrlResponse(string DownloadUrl, DateTime ExpiresAt);
