namespace ChildCare.Contracts.Responses;

public record StaffLeaveRequestResponse(
    Guid Id,
    Guid StaffProfileId,
    string Type,
    DateOnly DateFrom,
    DateOnly DateTo,
    string? Notes,
    string Status,
    Guid? DecidedBy,
    DateTime? DecidedAt,
    DateTime CreatedAt);
