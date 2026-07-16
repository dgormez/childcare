namespace ChildCare.Application.Common;

/// <summary>
/// Signed-URL port for a fiscal attestation's persisted PDF (research.md R1, feature 015).
/// Unlike IHealthAttachmentStorage/IProfilePhotoStorage (client-signed-upload, bytes never
/// touch the API), the PDF here is generated server-side (QuestPDF) and written directly —
/// mirrors IGroupActivityPhotoStorage's upload half (server-side StorageClient write) combined
/// with every existing Gcs*Storage port's download half (UrlSigner). No public URLs
/// (constitution's Secure Configuration & Storage principle).
/// </summary>
public interface IFiscalAttestationStorage
{
    /// <summary>Writes the PDF to a deterministic object path keyed on attestationId — a
    /// regenerate overwrites the same object rather than accumulating orphaned prior PDFs
    /// (mirrors GcsProfilePhotoStorage's deterministic-path precedent).</summary>
    Task<string> UploadAsync(Guid attestationId, byte[] pdfBytes, CancellationToken cancellationToken = default);

    Task<string> CreateDownloadUrlAsync(string objectPath, CancellationToken cancellationToken = default);
}
