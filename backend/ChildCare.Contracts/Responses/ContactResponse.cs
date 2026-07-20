namespace ChildCare.Contracts.Responses;

public record ContactResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Phone,
    string? Email,
    string Locale,
    // Feature 022 — every contact-reading route is DirectorOnly, so no role-gating needed here
    // (unlike ChildResponse; see ContactMapper).
    DateTime? IdVerifiedAt,
    string? IdVerifiedByEmail,
    string? IdDocumentType,
    string? IdDocumentNote,
    DateTime? FirstIdVerifiedAt,
    string? FirstIdVerifiedByEmail);

public record ChildContactResponse(
    Guid ContactId,
    string FirstName,
    string LastName,
    string Phone,
    string? Email,
    string Locale,
    string Relationship,
    bool CanPickup,
    bool IsPrimary,
    DateTime? IdVerifiedAt,
    string? IdVerifiedByEmail,
    string? IdDocumentType,
    string? IdDocumentNote,
    DateTime? FirstIdVerifiedAt,
    string? FirstIdVerifiedByEmail);
