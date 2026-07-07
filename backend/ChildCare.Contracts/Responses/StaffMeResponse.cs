namespace ChildCare.Contracts.Responses;

public record StaffMeResponse(
    Guid StaffProfileId,
    string FirstName,
    string LastName,
    string Role,
    IReadOnlyList<Guid> EligibleLocationIds);
