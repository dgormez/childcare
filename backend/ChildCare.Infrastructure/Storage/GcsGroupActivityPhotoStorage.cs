using ChildCare.Application.Common;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ChildCare.Infrastructure.Storage;

/// <summary>
/// IGroupActivityPhotoStorage implementation (research.md R2/R3). Unlike GcsProfilePhotoStorage,
/// this port resizes bytes in-process (SixLabors.ImageSharp) and writes both the resized full
/// image and its thumbnail directly to GCS via the API's own credentials — no client-side signed
/// PUT, since resize compute has to happen somewhere and a signed PUT URL bypasses the API
/// entirely. Reads still use signed URLs (UrlSigner), same as every other photo in this codebase.
/// Reuses the existing profile-photos bucket under a "group-activities/" path prefix (plan.md's
/// Storage decision) rather than provisioning a dedicated bucket.
/// </summary>
public class GcsGroupActivityPhotoStorage : IGroupActivityPhotoStorage
{
    private const int MaxLongEdgePixels = 1920;
    private const int ThumbnailLongEdgePixels = 400;
    private static readonly TimeSpan DownloadUrlDuration = TimeSpan.FromMinutes(15);

    private readonly string _bucketName;
    private readonly Lazy<Task<StorageClient>> _storageClient;
    private readonly Lazy<Task<UrlSigner>> _urlSigner;
    private readonly ILogger<GcsGroupActivityPhotoStorage> _logger;

    public GcsGroupActivityPhotoStorage(IConfiguration config, ILogger<GcsGroupActivityPhotoStorage> logger)
    {
        _bucketName = config["Storage:ProfilePhotosBucketName"]
            ?? throw new InvalidOperationException("Storage:ProfilePhotosBucketName is not configured.");
        _logger = logger;

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

    public async Task<(string ObjectPath, string ThumbnailObjectPath)> UploadAsync(
        Guid groupActivityId, Guid photoId, Stream imageBytes, CancellationToken cancellationToken = default)
    {
        var objectPath = $"group-activities/{groupActivityId}/{photoId}.jpg";
        var thumbnailObjectPath = $"group-activities/{groupActivityId}/{photoId}-thumb.jpg";

        using var image = await Image.LoadAsync(imageBytes, cancellationToken);

        var client = await _storageClient.Value;
        var encoder = new JpegEncoder { Quality = 85 };

        using (var fullImage = image.Clone(ctx => ResizeToLongEdge(ctx, MaxLongEdgePixels)))
        using (var fullStream = new MemoryStream())
        {
            await fullImage.SaveAsync(fullStream, encoder, cancellationToken);
            fullStream.Position = 0;
            await client.UploadObjectAsync(_bucketName, objectPath, "image/jpeg", fullStream, cancellationToken: cancellationToken);
        }

        using (var thumbImage = image.Clone(ctx => ResizeToLongEdge(ctx, ThumbnailLongEdgePixels)))
        using (var thumbStream = new MemoryStream())
        {
            await thumbImage.SaveAsync(thumbStream, encoder, cancellationToken);
            thumbStream.Position = 0;
            await client.UploadObjectAsync(_bucketName, thumbnailObjectPath, "image/jpeg", thumbStream, cancellationToken: cancellationToken);
        }

        return (objectPath, thumbnailObjectPath);
    }

    public async Task<string?> CreateDownloadUrlAsync(string? objectPath, CancellationToken cancellationToken = default)
    {
        if (objectPath is null)
            return null;

        var signer = await _urlSigner.Value;
        return await signer.SignAsync(_bucketName, objectPath, DownloadUrlDuration, cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(string objectPath, string thumbnailObjectPath, CancellationToken cancellationToken = default)
    {
        var client = await _storageClient.Value;

        // Best-effort: a missing GCS object shouldn't block the DB delete that already
        // guarantees the photo is gone from every parent/caregiver/director surface.
        foreach (var path in new[] { objectPath, thumbnailObjectPath })
        {
            try
            {
                await client.DeleteObjectAsync(_bucketName, path, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete GCS object {ObjectPath} for a deleted group activity photo.", path);
            }
        }
    }

    private static void ResizeToLongEdge(IImageProcessingContext ctx, int maxLongEdge)
    {
        var size = ctx.GetCurrentSize();
        if (size.Width <= maxLongEdge && size.Height <= maxLongEdge)
            return;

        ctx.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new SixLabors.ImageSharp.Size(maxLongEdge, maxLongEdge),
        });
    }
}
