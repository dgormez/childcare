using System.Net;
using System.Net.Http.Json;
using System.Text;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.FiscalAttestations.FiscalAttestationTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.FiscalAttestations;

/// <summary>Feature 015 — spec.md User Story 2, Security considerations. FR-006/FR-007/FR-011.</summary>
public class FiscalAttestationDownloadUrlTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task ParentDownloadUrl_ForNonExistentAttestation_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Download Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/fiscal-attestations/{Guid.NewGuid()}/download-url", parentToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ParentDownloadUrl_ForAttestationNotBelongingToParent_ReturnsIdentical404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Download Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var otherChild = await CreateChildAsync(client, org.AccessToken, "OtherChild");
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, otherChild.Id, 2027, 1);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/fiscal-attestations/generate", org.AccessToken, new GenerateFiscalAttestationsRequest(2027)));
        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/fiscal-attestations?taxYear=2027", org.AccessToken));
        var otherAttestation = (await listResponse.Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!.Single();

        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/fiscal-attestations/{otherAttestation.Id}/download-url", parentToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ParentDownloadUrl_ForOwnAttestation_ReturnsSignedUrl()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Download Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 1);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/fiscal-attestations/generate", org.AccessToken, new GenerateFiscalAttestationsRequest(2027)));
        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/fiscal-attestations?taxYear=2027", org.AccessToken));
        var attestation = (await listResponse.Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!.Single();

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/parent/fiscal-attestations/{attestation.Id}/download-url", parentToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FiscalAttestationDownloadUrlResponse>();
        Assert.False(string.IsNullOrEmpty(body!.DownloadUrl));
    }

    [Fact]
    public async Task Generate_ProducesAValidNonEmptyPdf()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Fiscal Download Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var child = await CreateChildAsync(client, org.AccessToken);
        await CreatePaidInvoiceAsync(client, org.AccessToken, factory.Services, org.Organisation.Id, location.Id, child.Id, 2027, 1);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/fiscal-attestations/generate", org.AccessToken, new GenerateFiscalAttestationsRequest(2027)));
        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/fiscal-attestations?taxYear=2027", org.AccessToken));
        var attestation = (await listResponse.Content.ReadFromJsonAsync<List<FiscalAttestationResponse>>())!.Single();

        var storage = factory.Services.GetRequiredService<FakeFiscalAttestationStorage>();
        var bytes = storage.Uploaded[attestation.Id.GetValueOrDefault()];

        // Mirrors InvoicePdfTests' exact validity assertion (014) — a real, non-empty PDF was
        // actually rendered and uploaded end-to-end, not just a status returned. FR-007's
        // "never a pre-filled NRN" is covered structurally by FiscalAttestationNoNrnFieldTests
        // (the model type has no field that could hold one) rather than by scanning PDF bytes,
        // which are a compressed binary format unsuitable for text-content assertions — same
        // posture this codebase's only other PDF test (InvoicePdfTests) already takes.
        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
