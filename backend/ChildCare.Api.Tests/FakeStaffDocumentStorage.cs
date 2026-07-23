using ChildCare.Application.Common;

namespace ChildCare.Api.Tests;

/// <summary>
/// Test double for IStaffDocumentStorage (feature 028, research.md R3) — registered Singleton
/// in OrganisationOnboardingWebAppFactory, overriding Program.cs's real
/// GcsStaffDocumentStorage, so tests never hit real GCS. Mirrors FakeHealthAttachmentStorage's
/// deterministic, non-expiring "signed" URL pattern.
/// </summary>
public class FakeStaffDocumentStorage : IStaffDocumentStorage
{
    public HashSet<string> DeletedPaths { get; } = [];

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "application/pdf" => "pdf",
        "image/jpeg" => "jpg",
        "image/png" => "png",
        _ => "bin",
    };

    public Task<(string ObjectPath, string UploadUrl)> CreateUploadUrlAsync(Guid staffProfileId, string contentType, string category = "staff-documents", CancellationToken cancellationToken = default)
    {
        var objectPath = $"{category}/{staffProfileId}/{Guid.NewGuid()}.{ExtensionFor(contentType)}";
        return Task.FromResult((objectPath, $"https://fake-gcs.test/upload/{objectPath}"));
    }

    public Task<string?> CreateDownloadUrlAsync(string? objectPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(objectPath is null ? null : $"https://fake-gcs.test/download/{objectPath}");

    public Task<string> CreateAttachmentDownloadUrlAsync(string objectPath, string downloadFileName, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://fake-gcs.test/download/{objectPath}?attachment={Uri.EscapeDataString(downloadFileName)}");

    public Task<bool> DeleteAsync(string objectPath, CancellationToken cancellationToken = default)
    {
        DeletedPaths.Add(objectPath);
        return Task.FromResult(true);
    }
}
