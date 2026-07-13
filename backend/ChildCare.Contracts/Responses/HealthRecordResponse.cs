namespace ChildCare.Contracts.Responses;

public record HealthRecordResponse(
    Guid Id,
    Guid ChildId,
    string RecordType,
    string Title,
    string Description,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    bool IsExpired,
    string? AttachmentDownloadUrl,
    Guid? RecordedBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record CreateHealthRecordAttachmentUploadUrlResponse(string UploadUrl, int ExpiresInSeconds);
