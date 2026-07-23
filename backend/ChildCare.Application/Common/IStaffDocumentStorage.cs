namespace ChildCare.Application.Common;

/// <summary>
/// Signed-URL port for staff HR documents (research.md R3, feature 028) — same idiom as
/// IHealthAttachmentStorage (subjectId + contentType + category → signed upload/download URL
/// pair, no public URLs, no bytes proxied through the API), mirrored rather than reused because
/// staff documents are a distinct document class stored under their own object-path prefix.
/// SetStorageClassAsync is deliberately omitted — feature 031's cost-tiering lifecycle is scoped
/// to child/staff-photo and health-attachment objects, not HR documents.
/// </summary>
public interface IStaffDocumentStorage
{
    /// <param name="contentType">Caller-validated (e.g. "application/pdf", "image/jpeg",
    /// "image/png"); this port trusts it.</param>
    /// <param name="category">Object-path prefix segment — defaults to "staff-documents".</param>
    Task<(string ObjectPath, string UploadUrl)> CreateUploadUrlAsync(Guid staffProfileId, string contentType, string category = "staff-documents", CancellationToken cancellationToken = default);

    /// <summary>Returns null when objectPath is null rather than signing a meaningless URL.</summary>
    Task<string?> CreateDownloadUrlAsync(string? objectPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs a time-limited download URL with an attachment (not inline) content-disposition, so
    /// the browser saves the file rather than rendering it — mirrors
    /// IHealthAttachmentStorage.CreateAttachmentDownloadUrlAsync.
    /// </summary>
    Task<string> CreateAttachmentDownloadUrlAsync(string objectPath, string downloadFileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the GCS object outright. Best-effort: a failure is logged and reported via a
    /// boolean return rather than thrown. Returns true if the object no longer exists afterward
    /// (including if it never existed — idempotent, safe to retry).
    /// </summary>
    Task<bool> DeleteAsync(string objectPath, CancellationToken cancellationToken = default);
}
