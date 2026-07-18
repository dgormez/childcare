namespace ChildCare.Contracts.Responses;

public record GroupResponse(
    Guid Id,
    string Name,
    Guid LocationId,
    int? Capacity);

public record ChildGroupAssignmentResponse(
    Guid GroupId,
    string GroupName,
    DateOnly StartDate,
    DateOnly? EndDate);
