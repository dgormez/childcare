using ChildCare.Application.Common;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;

namespace ChildCare.Infrastructure.Storage;

/// <summary>
/// IHealthAttachmentStorage implementation backed by GCS V4 signed URLs (research.md R2) —
/// reuses the same "Storage:ProfilePhotosBucketName" bucket as GcsProfilePhotoStorage (the
/// category path segment already isolates subject types, per that port's own precedent; no new
/// bucket or Terraform change needed).
/// </summary>
public class GcsHealthAttachmentStorage : IHealthAttachmentStorage
{
    private static readonly TimeSpan UploadUrlDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DownloadUrlDuration = TimeSpan.FromMinutes(15);

    private readonly string _bucketName;
    private readonly Lazy<Task<UrlSigner>> _urlSigner;

    public GcsHealthAttachmentStorage(IConfiguration config)
    {
        _bucketName = config["Storage:ProfilePhotosBucketName"]
            ?? throw new InvalidOperationException("Storage:ProfilePhotosBucketName is not configured.");

        _urlSigner = new Lazy<Task<UrlSigner>>(async () =>
        {
            var credential = await GoogleCredential.GetApplicationDefaultAsync();
            return UrlSigner.FromCredential(credential);
        });
    }

    public async Task<(string ObjectPath, string UploadUrl)> CreateUploadUrlAsync(Guid healthRecordId, string contentType, CancellationToken cancellationToken = default)
    {
        // Deterministic per health record — a re-upload overwrites the same object rather than
        // accumulating orphaned prior attachments (mirrors GcsProfilePhotoStorage's precedent).
        var extension = ExtensionFor(contentType);
        var objectPath = $"health-records/{healthRecordId}/attachment.{extension}";

        var signer = await _urlSigner.Value;
        var uploadUrl = await signer.SignAsync(
            _bucketName, objectPath, UploadUrlDuration, HttpMethod.Put, cancellationToken: cancellationToken);

        return (objectPath, uploadUrl);
    }

    public async Task<string?> CreateDownloadUrlAsync(string? objectPath, CancellationToken cancellationToken = default)
    {
        if (objectPath is null)
            return null;

        var signer = await _urlSigner.Value;
        return await signer.SignAsync(_bucketName, objectPath, DownloadUrlDuration, cancellationToken: cancellationToken);
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "application/pdf" => "pdf",
        "image/jpeg" => "jpg",
        "image/png" => "png",
        _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, "Unsupported attachment content type."),
    };
}
