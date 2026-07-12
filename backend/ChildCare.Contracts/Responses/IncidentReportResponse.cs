namespace ChildCare.Contracts.Responses;

public record IncidentReportResponse(
    Guid Id,
    Guid ChildId,
    Guid LocationId,
    DateTime OccurredAt,
    string? LocationDetail,
    string Description,
    string InjuryType,
    string? FirstAidGiven,
    bool DoctorCalled,
    string? DoctorNotes,
    bool ParentNotified,
    DateTime? ParentNotifiedAt,
    string? ParentNotifiedHow,
    IReadOnlyList<Guid> ReportedBy,
    string? Witnesses,
    string? FollowUp,
    DateTime? ReviewedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record PagedIncidentReportsResponse(
    IReadOnlyList<IncidentReportResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
