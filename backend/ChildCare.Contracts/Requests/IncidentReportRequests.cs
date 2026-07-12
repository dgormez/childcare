namespace ChildCare.Contracts.Requests;

public record FileIncidentReportRequest(
    Guid ChildId,
    DateTime? OccurredAt,
    string? LocationDetail,
    string Description,
    string InjuryType,
    string? FirstAidGiven,
    bool DoctorCalled,
    string? DoctorNotes,
    bool ParentNotified,
    DateTime? ParentNotifiedAt,
    string? ParentNotifiedHow,
    string? Witnesses,
    string? FollowUp);

// All fields optional (partial update) — FR-005/FR-006: a report older than 24 hours only
// accepts FollowUp; any other field present triggers a 409 (research.md/contracts).
public record UpdateIncidentReportRequest(
    DateTime? OccurredAt,
    string? LocationDetail,
    string? Description,
    string? InjuryType,
    string? FirstAidGiven,
    bool? DoctorCalled,
    string? DoctorNotes,
    bool? ParentNotified,
    DateTime? ParentNotifiedAt,
    string? ParentNotifiedHow,
    string? Witnesses,
    string? FollowUp);
