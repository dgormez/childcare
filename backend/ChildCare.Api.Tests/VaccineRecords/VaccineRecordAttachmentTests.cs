using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.VaccineRecords;

/// <summary>User Story 3 (spec.md FR-011/FR-012): attachment upload-url issuance sets a
/// download URL on the next read; an unsupported content type is rejected without affecting the
/// already-saved vaccine record.</summary>
public class VaccineRecordAttachmentTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static CreateVaccineRecordRequest MinimalRequest() =>
        new("DTP", 2, new DateOnly(2026, 6, 1), null, "Dr. Peeters", null);

    [Fact]
    public async Task AttachmentUploadUrl_ThenGet_ReturnsSignedDownloadUrl()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var created = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", org.AccessToken, MinimalRequest()));
        var record = (await created.Content.ReadFromJsonAsync<VaccineRecordResponse>())!;
        Assert.Null(record.AttachmentDownloadUrl);

        var uploadUrlResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records/{record.Id}/attachment-upload-url", org.AccessToken,
            new CreateVaccineRecordAttachmentUploadUrlRequest("image/jpeg")));
        Assert.Equal(HttpStatusCode.OK, uploadUrlResponse.StatusCode);
        var uploadUrlBody = (await uploadUrlResponse.Content.ReadFromJsonAsync<CreateVaccineRecordAttachmentUploadUrlResponse>())!;
        Assert.Contains("attachment.jpg", uploadUrlBody.UploadUrl);
        Assert.Contains("vaccine-records/", uploadUrlBody.UploadUrl); // distinct object-path prefix, research.md R4

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/vaccine-records", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<VaccineRecordResponse>>())!;
        var updated = list.Single(v => v.Id == record.Id);
        Assert.NotNull(updated.AttachmentDownloadUrl);
    }

    [Fact]
    public async Task AttachmentUploadUrl_UnsupportedContentType_ReturnsUnprocessableEntity_AndRecordUnaffected()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Vaccine Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var created = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", org.AccessToken, MinimalRequest()));
        var record = (await created.Content.ReadFromJsonAsync<VaccineRecordResponse>())!;

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records/{record.Id}/attachment-upload-url", org.AccessToken,
            new CreateVaccineRecordAttachmentUploadUrlRequest("application/zip")));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        // The vaccine record itself remains saved and unaffected (spec.md FR-012).
        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/vaccine-records", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<VaccineRecordResponse>>())!;
        var stillThere = list.Single(v => v.Id == record.Id);
        Assert.Equal("DTP", stillThere.VaccineName);
        Assert.Null(stillThere.AttachmentDownloadUrl);
    }
}
