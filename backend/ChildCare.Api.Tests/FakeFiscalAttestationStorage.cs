using System.Collections.Concurrent;
using ChildCare.Application.Common;

namespace ChildCare.Api.Tests;

/// <summary>
/// Test double for IFiscalAttestationStorage (feature 015) — registered Singleton in
/// OrganisationOnboardingWebAppFactory, overriding Program.cs's real GcsFiscalAttestationStorage,
/// so tests never hit real GCS (constitution Principle V — same external-service seam
/// FakeGroupActivityPhotoStorage/FakeHealthAttachmentStorage already fake). Unlike those two
/// (which only need to fake the URL), fiscal attestations are backend-rendered and persisted —
/// tests need to inspect the actual uploaded bytes to prove a real PDF was produced
/// end-to-end, so this fake captures them in memory, keyed by attestationId.
/// </summary>
public class FakeFiscalAttestationStorage : IFiscalAttestationStorage
{
    public ConcurrentDictionary<Guid, byte[]> Uploaded { get; } = new();

    public Task<string> UploadAsync(Guid attestationId, byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        Uploaded[attestationId] = pdfBytes;
        return Task.FromResult($"fiscal-attestations/{attestationId}.pdf");
    }

    public Task<string> CreateDownloadUrlAsync(string objectPath, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://fake-gcs.test/download/{objectPath}");
}
