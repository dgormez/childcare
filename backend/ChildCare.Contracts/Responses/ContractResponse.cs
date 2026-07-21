namespace ChildCare.Contracts.Responses;

public record ContractResponse(
    Guid Id,
    Guid ChildId,
    Guid LocationId,
    Guid? PreviousContractId,
    DateOnly StartDate,
    DateOnly? EndDate,
    IReadOnlyList<ContractedDayResponse> ContractedDays,
    int DailyRateCents,
    string Status,
    ContractConsentResponse Consent,
    // Feature 024-esignature — derived (not_sent/pending/expired/signed), never a raw timestamp
    // read (FR-018).
    string SigningStatus,
    DateTime? SignedAt,
    // Masked (e.g. last 4 digits only) — the decrypted IBAN is never returned in full after
    // capture (FR-020).
    string? SepaIbanMasked,
    string? SepaMandateReference);

public record ContractedDayResponse(
    DayOfWeek Weekday,
    TimeOnly StartTime,
    TimeOnly EndTime);

public record ContractConsentResponse(
    bool PhotosInternal,
    bool PhotosWebsite,
    bool PhotosSocialMedia,
    bool VideoInternal,
    bool PhotosPress);
