namespace ChildCare.Contracts.Requests;

public record ContractedDayRequest(
    DayOfWeek Weekday,
    TimeOnly StartTime,
    TimeOnly EndTime);

public record ContractConsentRequest(
    bool PhotosInternal,
    bool PhotosWebsite,
    bool PhotosSocialMedia,
    bool VideoInternal,
    bool PhotosPress);

public record CreateContractRequest(
    Guid LocationId,
    // Nullable so a request that omits startDate is distinguishable from one supplying the
    // default(DateOnly) value — makes errors.contract.start_date_required actually reachable.
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<ContractedDayRequest> ContractedDays,
    int DailyRateCents,
    ContractConsentRequest? Consent);

public record UpdateContractRequest(
    DateOnly? StartDate,
    DateOnly? EndDate,
    IReadOnlyList<ContractedDayRequest> ContractedDays,
    int DailyRateCents,
    ContractConsentRequest? Consent);

public record AmendContractRequest(
    DateOnly EffectiveStartDate,
    Guid LocationId,
    DateOnly? EndDate,
    IReadOnlyList<ContractedDayRequest> ContractedDays,
    int DailyRateCents,
    ContractConsentRequest? Consent);

public record TerminateContractRequest(
    DateOnly EndDate);
