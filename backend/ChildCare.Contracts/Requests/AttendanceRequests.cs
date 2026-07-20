namespace ChildCare.Contracts.Requests;

// Named Attendance*Request (not CheckInRequest/CheckOutRequest) — RoomShiftEndpoints.cs already
// declares distinct types with those exact short names (StaffId/Pin shape). ASP.NET Core's
// OpenAPI schema components are keyed by simple type name, so two C# types sharing a name
// silently clobber each other's generated schema — discovered when openapi-typescript produced
// a mobile client typed against the wrong shape entirely.
//
// LocationId/GroupId are sourced from the recording device's own JWT claims (endpoint layer
// resolves them, mirrors RecordChildEventRequest's convention) — never client-supplied here.
public record AttendanceCheckInRequest(Guid ChildId, DateOnly Date);

public record AttendanceCheckOutRequest(Guid ChildId, DateOnly Date);

// Dual-auth route (device or director) — LocationId/GroupId are explicit body fields since a
// director JWT carries no device location claim to derive them from; for a device-authenticated
// caller, the endpoint layer overrides both from the device's own claims regardless of what the
// body supplies.
public record MarkAbsentRequest(
    Guid ChildId, Guid LocationId, Guid? GroupId, DateOnly Date, bool AbsenceJustified, string? AbsenceReason);

public record CorrectAttendanceRequest(
    string? Status, DateTime? CheckInAt, DateTime? CheckOutAt, bool? AbsenceJustified, string? AbsenceReason);

// Feature 021 — contracts/021-qr-checkin/qr-checkin-api.md.
public record IssueCheckInCodeRequest(Guid ChildId);

public record VerifyCheckInCodeRequest(string Code);
