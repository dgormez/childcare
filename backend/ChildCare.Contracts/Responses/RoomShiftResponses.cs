namespace ChildCare.Contracts.Responses;

public record DevicePairingResponse(
    Guid DeviceId,
    string DeviceToken,
    int TokenVersion);

/// <summary>Feature 007a (spec.md FR-013a) — read-only summary for the director web Devices
/// screen. Names are resolved server-side (mirrors ListStaffQuery's photo-URL resolution
/// pattern) rather than pushing id-to-name joins onto the client.</summary>
public record DeviceSummaryResponse(
    Guid Id,
    Guid LocationId,
    string LocationName,
    Guid GroupId,
    string GroupName,
    Guid PairedByTenantUserId,
    string PairedByName,
    DateTime PairedAt,
    DateTime? RevokedAt);

public record RoomRosterCardResponse(
    Guid StaffProfileId,
    string FirstName,
    string? PhotoUrl,
    bool CheckedIn,
    DateTime? CheckedInAt);

/// <summary>Feature 008b: wraps the roster with the location's current RequiresCaregiverPin
/// setting, fetched alongside the existing roster call rather than a separate request.</summary>
public record RoomRosterResponse(
    bool RequiresCaregiverPin,
    IReadOnlyList<RoomRosterCardResponse> Caregivers);

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
