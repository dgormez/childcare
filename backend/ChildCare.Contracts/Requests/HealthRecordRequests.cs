namespace ChildCare.Contracts.Requests;

public record CreateHealthRecordRequest(
    string RecordType,
    string Title,
    string Description,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil);

public record UpdateHealthRecordRequest(
    string RecordType,
    string Title,
    string Description,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil);

public record CreateHealthRecordAttachmentUploadUrlRequest(string ContentType);
