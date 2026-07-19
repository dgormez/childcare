namespace ChildCare.Contracts.Requests;

public record BulkEmailAttachmentUploadUrlRequest(string ContentType);

public record SendBulkEmailRequest(
    Guid LocationId,
    Guid? GroupId,
    string Subject,
    string Body,
    string? AttachmentObjectPath,
    string? AttachmentFileName,
    string? AttachmentContentType);

public record UnsubscribeDigestRequest(string Token, string Org);
