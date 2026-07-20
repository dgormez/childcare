using ChildCare.Application.Common;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ChildCare.Infrastructure.Storage;

/// <summary>
/// IProfilePhotoStorage implementation backed by GCS V4 signed URLs (research.md R3). Bucket
/// name and credentials come from configuration/ADC (constitution Principle VI — never
/// hardcoded). On Cloud Run, UrlSigner.FromCredential signs via the IAM Credentials API's
/// signBlob endpoint against the runtime service account's own identity — no downloaded key
/// file needed — which is why infra/gcp/main.tf grants that service account
/// roles/iam.serviceAccountTokenCreator on itself.
/// </summary>
public class GcsProfilePhotoStorage : IProfilePhotoStorage
{
    private static readonly TimeSpan UploadUrlDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DownloadUrlDuration = TimeSpan.FromMinutes(15);

    private readonly string _bucketName;
    private readonly Lazy<Task<UrlSigner>> _urlSigner;
    private readonly Lazy<Task<StorageClient>> _storageClient;
    private readonly ILogger<GcsProfilePhotoStorage> _logger;

    public GcsProfilePhotoStorage(IConfiguration config, ILogger<GcsProfilePhotoStorage> logger)
    {
        // Renamed from Storage:StaffPhotosBucketName in feature 006-children — this single
        // bucket now serves both staff and child photos, distinguished by the category path
        // segment (research.md R1), so a staff-specific config key name would be misleading.
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

    public async Task<(string ObjectPath, string UploadUrl)> CreateUploadUrlAsync(string category, Guid subjectId, CancellationToken cancellationToken = default)
    {
        // Deterministic per subject — a re-upload overwrites the same object rather than
        // accumulating orphaned prior photos (feature 005 FR-013, spec.md Assumptions).
        var objectPath = $"{category}/{subjectId}/photo.jpg";

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
        // GCS honors response-content-disposition as a signed query parameter (same mechanism
        // S3 uses) — no dedicated "options.ResponseDisposition" property exists on this SDK's
        // UrlSigner, so it must ride along as a RequestTemplate query parameter instead.
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
            // Already gone — treated as satisfied so a retried purge never reports a stale
            // failure (031-photo-lifecycle-governance FR-016).
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
}
