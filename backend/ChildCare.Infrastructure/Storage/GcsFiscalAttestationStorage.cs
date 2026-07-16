using ChildCare.Application.Common;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;

namespace ChildCare.Infrastructure.Storage;

/// <summary>
/// IFiscalAttestationStorage implementation (research.md R1) — writes the QuestPDF-rendered
/// bytes directly to GCS via the API's own credentials (mirrors GcsGroupActivityPhotoStorage's
/// upload half: server-generated content, no client-signed PUT), and reads via UrlSigner
/// (mirrors every existing Gcs*Storage port's download half). Reuses the existing
/// "Storage:ProfilePhotosBucketName" bucket under a "fiscal-attestations/" path prefix — same
/// bucket-reuse-via-prefix precedent GcsHealthAttachmentStorage/GcsGroupActivityPhotoStorage
/// already established, no new bucket/Terraform change.
/// </summary>
public class GcsFiscalAttestationStorage : IFiscalAttestationStorage
{
    private static readonly TimeSpan DownloadUrlDuration = TimeSpan.FromMinutes(15);

    private readonly string _bucketName;
    private readonly Lazy<Task<StorageClient>> _storageClient;
    private readonly Lazy<Task<UrlSigner>> _urlSigner;

    public GcsFiscalAttestationStorage(IConfiguration config)
    {
        _bucketName = config["Storage:ProfilePhotosBucketName"]
            ?? throw new InvalidOperationException("Storage:ProfilePhotosBucketName is not configured.");

        _storageClient = new Lazy<Task<StorageClient>>(async () =>
        {
            var credential = await GoogleCredential.GetApplicationDefaultAsync();
            return await StorageClient.CreateAsync(credential);
        });

        _urlSigner = new Lazy<Task<UrlSigner>>(async () =>
        {
            var credential = await GoogleCredential.GetApplicationDefaultAsync();
            return UrlSigner.FromCredential(credential);
        });
    }

    public async Task<string> UploadAsync(Guid attestationId, byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        // Deterministic per attestation — a regenerate overwrites the same object rather than
        // accumulating orphaned prior PDFs (mirrors GcsProfilePhotoStorage's precedent).
        var objectPath = $"fiscal-attestations/{attestationId}.pdf";

        var client = await _storageClient.Value;
        using var stream = new MemoryStream(pdfBytes);
        await client.UploadObjectAsync(_bucketName, objectPath, "application/pdf", stream, cancellationToken: cancellationToken);

        return objectPath;
    }

    public async Task<string> CreateDownloadUrlAsync(string objectPath, CancellationToken cancellationToken = default)
    {
        var signer = await _urlSigner.Value;
        return await signer.SignAsync(_bucketName, objectPath, DownloadUrlDuration, cancellationToken: cancellationToken);
    }
}
