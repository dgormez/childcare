namespace ChildCare.Application.Common;

/// <summary>
/// Signed-URL port for a health record's optional attachment (research.md R2, feature 013c) —
/// same idiom as IProfilePhotoStorage (category + subjectId → signed upload/download URL pair,
/// no public URLs, no bytes proxied through the API), but a new port rather than an extension of
/// IProfilePhotoStorage: that interface hardcodes a ".jpg" object path, appropriate for "exactly
/// one photo," wrong for an attachment whose file type varies (PDF/JPEG/PNG).
/// </summary>
public interface IHealthAttachmentStorage
{
    /// <param name="contentType">One of "application/pdf", "image/jpeg", "image/png" — the
    /// caller validates this before calling (FR-006); this port trusts it.</param>
    /// <param name="category">Object-path prefix segment (research.md R4, feature 013g) —
    /// defaults to "health-records" so every pre-existing call site is unaffected; vaccine
    /// records pass "vaccine-records" to keep the two attachment kinds in distinct paths within
    /// the same bucket.</param>
    Task<(string ObjectPath, string UploadUrl)> CreateUploadUrlAsync(Guid healthRecordId, string contentType, string category = "health-records", CancellationToken cancellationToken = default);

    /// <summary>Returns null when objectPath is null (no attachment set) rather than signing a meaningless URL.</summary>
    Task<string?> CreateDownloadUrlAsync(string? objectPath, CancellationToken cancellationToken = default);
}
