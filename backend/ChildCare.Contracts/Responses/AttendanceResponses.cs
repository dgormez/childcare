namespace ChildCare.Contracts.Responses;

public record AttendanceRecordResponse(
    Guid Id,
    Guid ChildId,
    Guid LocationId,
    DateOnly Date,
    string Status,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    int? PlannedDurationMinutes,
    bool? AbsenceJustified,
    string? AbsenceReason,
    IReadOnlyList<Guid> RecordedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record PagedAttendanceResponse(
    IReadOnlyList<AttendanceRecordResponse> Items,
    // Opaque cursor for the next page (research.md R8); null = no more pages.
    string? NextCursor);

public record BkrRatioResponse(
    int PresentCount,
    int QualifiedStaffCount,
    bool IsNapTime,
    int Threshold,
    // "green" | "amber" | "red" — FR-007e's precise threshold comparison, never a UI-computed value.
    string Status);

// Feature 021 — contracts/021-qr-checkin/qr-checkin-api.md.
public record IssueCheckInCodeResponse(string Code, long ExpiresAtUnix);

// "check-in" | "check-out" — lets the caregiver tablet pick the right confirmation copy without
// re-deriving it from the record's own before/after state.
public record VerifyCheckInCodeResponse(AttendanceRecordResponse Attendance, string Direction);
