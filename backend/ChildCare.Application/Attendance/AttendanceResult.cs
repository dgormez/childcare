using ChildCare.Contracts.Responses;

namespace ChildCare.Application.Attendance;

public class AttendanceResult
{
    public AttendanceRecordResponse? Response { get; private init; }
    public bool Created { get; private init; }
    public AttendanceFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static AttendanceResult Success(AttendanceRecordResponse response, bool created) =>
        new() { Response = response, Created = created };

    public static AttendanceResult Fail(AttendanceFailure failure) => new() { Failure = failure };
}

public enum AttendanceFailure
{
    ChildNotFound,
    NotFound,

    // FR-003/FR-012: an existing status=present record already occupies this child/location/date.
    AlreadyRecorded,

    // FR-015: an existing record for this child/location/date is status=closure.
    ClosureDay,

    // FR-010/FR-011: caregiver correction outside the same-day/own-location window.
    EditWindowExpired,

    // FR-015: a correction attempted to set status=closure directly.
    ClosureStatusImmutable,
}
