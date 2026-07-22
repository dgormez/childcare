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
    string? SepaMandateReference,
    // Feature 026 — derived (none/signed/revoked), mirrors SigningStatus's own derived-field
    // precedent above rather than exposing a raw timestamp read.
    string MandateStatus,
    DateTime? SepaRevokedAt);

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

// Feature 024-esignature (User Story 2) — one row of the org-wide contracts list
// (ListContractsQuery), with just enough denormalized data (child/location name) to render
// without an extra round trip per row.
public record ContractSummaryResponse(
    Guid Id,
    Guid ChildId,
    string ChildName,
    string LocationName,
    DateOnly StartDate,
    int DailyRateCents,
    string Status,
    string SigningStatus,
    DateTime? SignedAt,
    // Feature 026 — none/signed/revoked, lets director-web offer the revoke action (FR-011)
    // from the same org-wide list this feature's send/resend action already lives on.
    string MandateStatus);
