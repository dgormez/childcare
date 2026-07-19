using ChildCare.Application.Common;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;

namespace ChildCare.Infrastructure.Storage;

/// <summary>
/// IBulkEmailAttachmentStorage implementation (feature 020, research.md R3) — signed upload URL
/// (client uploads directly, mirrors GcsHealthAttachmentStorage) plus a direct-bytes download via
/// the API's own credentials (StorageClient, mirrors GcsGroupActivityPhotoStorage) since the
/// bytes are needed server-side to attach to an outbound MIME message, not to hand back to a
/// browser. Reuses the existing profile-photos bucket under a "bulk-email-attachments/" prefix.
/// </summary>
public class GcsBulkEmailAttachmentStorage : IBulkEmailAttachmentStorage
{
    private static readonly TimeSpan UploadUrlDuration = TimeSpan.FromMinutes(15);

    private readonly string _bucketName;
    private readonly Lazy<Task<UrlSigner>> _urlSigner;
    private readonly Lazy<Task<StorageClient>> _storageClient;

    public GcsBulkEmailAttachmentStorage(IConfiguration config)
    {
        _bucketName = config["Storage:ProfilePhotosBucketName"]
            ?? throw new InvalidOperationException("Storage:ProfilePhotosBucketName is not configured.");

        _urlSigner = new Lazy<Task<UrlSigner>>(async () =>
        {
            var credential = await GoogleCredential.GetApplicationDefaultAsync();
            return UrlSigner.FromCredential(credential);
        });

        _storageClient = new Lazy<Task<StorageClient>>(async () =>
        {
            var credential = await GoogleCredential.GetApplicationDefaultAsync();
            return await StorageClient.CreateAsync(credential);
        });
    }

    public async Task<(string ObjectPath, string UploadUrl)> CreateUploadUrlAsync(Guid bulkEmailSendId, string contentType, CancellationToken cancellationToken = default)
    {
        var extension = ExtensionFor(contentType);
        var objectPath = $"bulk-email-attachments/{bulkEmailSendId}/attachment.{extension}";

        var signer = await _urlSigner.Value;
        var uploadUrl = await signer.SignAsync(
            _bucketName, objectPath, UploadUrlDuration, HttpMethod.Put, cancellationToken: cancellationToken);

        return (objectPath, uploadUrl);
    }

    public async Task<byte[]> DownloadBytesAsync(string objectPath, CancellationToken cancellationToken = default)
    {
        var client = await _storageClient.Value;
        using var stream = new MemoryStream();
        await client.DownloadObjectAsync(_bucketName, objectPath, stream, cancellationToken: cancellationToken);
        return stream.ToArray();
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "application/pdf" => "pdf",
        "image/jpeg" => "jpg",
        "image/png" => "png",
        _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, "Unsupported attachment content type."),
    };
}
