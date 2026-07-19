using ChildCare.Application.Common;

namespace ChildCare.Api.Tests;

/// <summary>
/// Test double for IBulkEmailAttachmentStorage (feature 020, research.md R3) — registered
/// Singleton in OrganisationOnboardingWebAppFactory, overriding Program.cs's real
/// GcsBulkEmailAttachmentStorage, so tests never hit real GCS. A test can't PUT to the fake
/// signed upload URL the way a browser would, so `SeedObject` stands in for that upload step —
/// call it with the `objectPath` returned by the upload-url endpoint before sending.
/// </summary>
public class FakeBulkEmailAttachmentStorage : IBulkEmailAttachmentStorage
{
    private readonly Dictionary<string, byte[]> _objects = [];

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "application/pdf" => "pdf",
        "image/jpeg" => "jpg",
        "image/png" => "png",
        _ => "bin",
    };

    public Task<(string ObjectPath, string UploadUrl)> CreateUploadUrlAsync(Guid bulkEmailSendId, string contentType, CancellationToken cancellationToken = default)
    {
        var objectPath = $"bulk-email-attachments/{bulkEmailSendId}/attachment.{ExtensionFor(contentType)}";
        return Task.FromResult((objectPath, $"https://fake-gcs.test/upload/{objectPath}"));
    }

    public Task<byte[]> DownloadBytesAsync(string objectPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(_objects.TryGetValue(objectPath, out var bytes) ? bytes : []);

    public void SeedObject(string objectPath, byte[] bytes) => _objects[objectPath] = bytes;
}
