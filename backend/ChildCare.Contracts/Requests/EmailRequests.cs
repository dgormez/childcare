namespace ChildCare.Contracts.Requests;

public record BulkEmailAttachmentUploadUrlRequest(string ContentType);

public record SendBulkEmailRequest(
    Guid LocationId,
    Guid? GroupId,
    string Subject,
    string Body,
    string? AttachmentObjectPath,
    string? AttachmentFileName,
    string? AttachmentContentType,
    // Applied to every individual recipient email in the batch (director sends one email per
    // household, not one email to many "To" addresses) — e.g. looping in a co-director.
    IReadOnlyList<string>? Cc,
    IReadOnlyList<string>? Bcc);

public record UnsubscribeDigestRequest(string Token, string Org);
