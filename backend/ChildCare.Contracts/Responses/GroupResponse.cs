namespace ChildCare.Contracts.Responses;

public record GroupResponse(
    Guid Id,
    string Name,
    Guid LocationId);

public record ChildGroupAssignmentResponse(
    Guid GroupId,
    string GroupName,
    DateOnly StartDate,
    DateOnly? EndDate);

public record VaccinationResponse(
    Guid Id,
    string VaccineName,
    DateOnly DateAdministered,
    DateOnly? NextDueDate,
    bool IsDue);
