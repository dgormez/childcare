namespace ChildCare.Contracts.Requests;

public record CreateStaffDocumentUploadUrlRequest(string ContentType);

public record CreateStaffDocumentRequest(string DocumentType, string Title, string ObjectPath, DateOnly? ValidFrom, DateOnly? ValidUntil);
