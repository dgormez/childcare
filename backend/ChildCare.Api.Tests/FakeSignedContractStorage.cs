using System.Collections.Concurrent;
using ChildCare.Application.Common;

namespace ChildCare.Api.Tests;

/// <summary>
/// Test double for ISignedContractStorage (feature 024-esignature) — registered Singleton in
/// OrganisationOnboardingWebAppFactory, overriding Program.cs's real GcsSignedContractStorage,
/// so tests never hit real GCS (constitution Principle V, mirrors FakeFiscalAttestationStorage).
/// Captures uploaded bytes in memory, keyed by contractId, so tests can assert a real signed PDF
/// was produced end-to-end (e.g. that it embeds the signature/SEPA mandate, not just any bytes).
/// </summary>
public class FakeSignedContractStorage : ISignedContractStorage
{
    public ConcurrentDictionary<Guid, byte[]> Uploaded { get; } = new();

    /// <summary>Contract ids this test run should simulate an upload failure for
    /// (SubmitContractSigningCommandHandler's compensating-restore path).</summary>
    public HashSet<Guid> ThrowOnUploadFor { get; } = [];

    public Task<string> UploadAsync(Guid contractId, byte[] pdfBytes, CancellationToken cancellationToken = default)
    {
        if (ThrowOnUploadFor.Contains(contractId))
            throw new InvalidOperationException("Simulated storage failure (test).");

        Uploaded[contractId] = pdfBytes;
        return Task.FromResult($"signed-contracts/{contractId}.pdf");
    }

    public Task<string> CreateDownloadUrlAsync(string objectPath, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://fake-gcs.test/download/{objectPath}");
}
