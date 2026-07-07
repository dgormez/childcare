namespace ChildCare.Contracts.Responses;

public record ContactResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Phone,
    string? Email,
    string Locale);

public record ChildContactResponse(
    Guid ContactId,
    string FirstName,
    string LastName,
    string Phone,
    string? Email,
    string Locale,
    string Relationship,
    bool CanPickup,
    bool IsPrimary);
