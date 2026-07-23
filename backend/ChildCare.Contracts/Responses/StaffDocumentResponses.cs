namespace ChildCare.Contracts.Responses;

public record StaffDocumentResponse(
    Guid Id,
    Guid StaffProfileId,
    string DocumentType,
    string Title,
    string? DownloadUrl,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    DateTime CreatedAt);

public record ContractExpiringResponse(Guid StaffProfileId, string StaffName, DateOnly ValidUntil, bool IsExpired);
