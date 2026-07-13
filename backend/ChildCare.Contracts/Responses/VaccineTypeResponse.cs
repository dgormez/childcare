namespace ChildCare.Contracts.Responses;

public record VaccineTypeResponse(
    Guid Id,
    string Name,
    string? Category,
    int SortOrder);
