namespace ChildCare.Application.Common;

/// <summary>
/// Signed-URL port for a bulk email's optional attachment (feature 020, research.md R3) — same
/// idiom as IHealthAttachmentStorage (subject-id → signed upload URL, no public URLs), but a new
/// port rather than an extension of it: that interface's methods are keyed on `healthRecordId`,
/// semantically wrong for a `BulkEmailSend` subject.
/// </summary>
public interface IBulkEmailAttachmentStorage
{
    /// <param name="contentType">One of "application/pdf", "image/jpeg", "image/png" — the
    /// caller validates this before calling; this port trusts it.</param>
    Task<(string ObjectPath, string UploadUrl)> CreateUploadUrlAsync(Guid bulkEmailSendId, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the attachment's raw bytes — used both to verify the 10MB size cap
    /// server-side (a signed upload URL cannot itself cap the byte count a client sends) and to
    /// attach the file to the outbound MIME message (data-model.md, research.md R3).
    /// </summary>
    Task<byte[]> DownloadBytesAsync(string objectPath, CancellationToken cancellationToken = default);
}
