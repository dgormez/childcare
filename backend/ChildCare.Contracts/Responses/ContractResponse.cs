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
    ContractConsentResponse Consent);

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
