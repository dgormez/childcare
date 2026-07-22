namespace ChildCare.Contracts.Responses;

// Feature 024-esignature. Same field set ContractPdfModel already renders (research.md R7) —
// nothing beyond what the presented token already authorizes for this one contract (FR-021).
public record ContractForSigningResponse(
    string ChildName,
    string LocationName,
    IReadOnlyList<ContractedDayResponse> ContractedDays,
    int DailyRateCents,
    ContractConsentResponse Consent,
    string Locale);
