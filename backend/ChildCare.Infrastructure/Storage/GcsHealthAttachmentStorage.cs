using ChildCare.Application.Common;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
    private readonly Lazy<Task<StorageClient>> _storageClient;
    private readonly ILogger<GcsHealthAttachmentStorage> _logger;

    public GcsHealthAttachmentStorage(IConfiguration config, ILogger<GcsHealthAttachmentStorage> logger)
    {
        _bucketName = config["Storage:ProfilePhotosBucketName"]
            ?? throw new InvalidOperationException("Storage:ProfilePhotosBucketName is not configured.");
        _logger = logger;

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

    public async Task<(string ObjectPath, string UploadUrl)> CreateUploadUrlAsync(Guid healthRecordId, string contentType, string category = "health-records", CancellationToken cancellationToken = default)
    {
        // Deterministic per record — a re-upload overwrites the same object rather than
        // accumulating orphaned prior attachments (mirrors GcsProfilePhotoStorage's precedent).
        // `category` distinguishes health-record vs. vaccine-record attachments within the same
        // bucket (research.md R4, feature 013g) — no new bucket/Terraform change needed.
        var extension = ExtensionFor(contentType);
        var objectPath = $"{category}/{healthRecordId}/attachment.{extension}";

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

    public async Task<string> CreateAttachmentDownloadUrlAsync(string objectPath, string downloadFileName, CancellationToken cancellationToken = default)
    {
        var signer = await _urlSigner.Value;
        var template = UrlSigner.RequestTemplate.FromBucket(_bucketName)
            .WithObjectName(objectPath)
            .WithQueryParameters(new[]
            {
                new KeyValuePair<string, IEnumerable<string>>(
                    "response-content-disposition", [$"attachment; filename=\"{downloadFileName}\""]),
            });
        var options = UrlSigner.Options.FromDuration(DownloadUrlDuration);
        return await signer.SignAsync(template, options, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string objectPath, CancellationToken cancellationToken = default)
    {
        var client = await _storageClient.Value;
        try
        {
            await client.DeleteObjectAsync(_bucketName, objectPath, cancellationToken: cancellationToken);
            return true;
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete GCS object {ObjectPath}.", objectPath);
            return false;
        }
    }

    public async Task SetStorageClassAsync(string objectPath, string storageClass, CancellationToken cancellationToken = default)
    {
        var client = await _storageClient.Value;
        var existing = await client.GetObjectAsync(_bucketName, objectPath, cancellationToken: cancellationToken);
        if (string.Equals(existing.StorageClass, storageClass, StringComparison.OrdinalIgnoreCase))
            return;

        existing.StorageClass = storageClass;
        await client.UpdateObjectAsync(existing, cancellationToken: cancellationToken);
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "application/pdf" => "pdf",
        "image/jpeg" => "jpg",
        "image/png" => "png",
        _ => throw new ArgumentOutOfRangeException(nameof(contentType), contentType, "Unsupported attachment content type."),
    };
}
