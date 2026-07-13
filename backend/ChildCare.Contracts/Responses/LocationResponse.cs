namespace ChildCare.Contracts.Responses;

public record LocationResponse(
    Guid Id,
    string Name,
    string Address,
    string Phone,
    string Email,
    int MaxCapacity,
    string? NaamLocatie,
    string? Dossiernummer,
    string? Verantwoordelijke,
    bool FlexPermission,
    bool BoPermission,
    DateTime? DeactivatedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string ReservationAbsencesMode,
    string ReservationExtrasMode,
    string ReservationSwapsMode,
    int ReservationNoticeHours,
    bool RequiresCaregiverPin);
