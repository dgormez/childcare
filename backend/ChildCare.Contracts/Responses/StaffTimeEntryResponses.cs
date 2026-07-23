namespace ChildCare.Contracts.Responses;

public record StaffTimeEntryResponse(
    Guid Id,
    Guid StaffProfileId,
    Guid LocationId,
    Guid? GroupId,
    DateTime ClockedInAt,
    DateTime? ClockedOutAt,
    string Function,
    string? Notes,
    bool IsOpen,
    bool IsLocked,
    DateTime? UnlockedAt);
