namespace ChildCare.Contracts.Requests;

public record CreateLocationRequest(
    string Name,
    string Address,
    string Phone,
    string Email,
    int MaxCapacity);

public record UpdateLocationRequest(
    string Name,
    string Address,
    string Phone,
    string Email,
    int MaxCapacity,
    string? NaamLocatie,
    string? Dossiernummer,
    string? Verantwoordelijke,
    bool FlexPermission,
    bool BoPermission);

public record UpdateLocationReservationSettingsRequest(
    string AbsencesMode,
    string ExtrasMode,
    string SwapsMode,
    int NoticeHours,
    bool ConfirmDespitePending);
