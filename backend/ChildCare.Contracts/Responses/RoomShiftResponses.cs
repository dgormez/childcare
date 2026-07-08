namespace ChildCare.Contracts.Responses;

public record DevicePairingResponse(
    Guid DeviceId,
    string DeviceToken,
    int TokenVersion);

public record RoomRosterCardResponse(
    Guid StaffProfileId,
    string FirstName,
    string? PhotoUrl,
    bool CheckedIn,
    DateTime? CheckedInAt);

public record CheckInResponse(
    Guid StaffProfileId,
    string FirstName,
    DateTime CheckedInAt);

public record CheckOutResponse(
    Guid StaffProfileId,
    string FirstName,
    DateTime CheckedOutAt);

public record ConfirmAdministratorResponse(
    Guid? AdministeredByStaffProfileId);

public record RoomShiftCorrectionResponse(
    Guid Id,
    Guid StaffProfileId,
    DateTime CheckedInAt,
    DateTime? CheckedOutAt,
    string? ClosedReason);
