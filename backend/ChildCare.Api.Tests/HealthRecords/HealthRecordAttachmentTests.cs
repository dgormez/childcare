using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.HealthRecords;

/// <summary>User Story 2 (spec.md FR-006/FR-007): attachment upload-url issuance sets a
/// download URL on the next read; a record with zero attachment calls still saves successfully
/// (attachments are always optional).</summary>
public class HealthRecordAttachmentTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task CreateHealthRecord_NoAttachment_SavesWithNullDownloadUrl()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Health Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var created = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("doctor_note", "Referral", "See attached letter.", null, null)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var record = (await created.Content.ReadFromJsonAsync<HealthRecordResponse>())!;
        Assert.Null(record.AttachmentDownloadUrl);
    }

    [Fact]
    public async Task AttachmentUploadUrl_ThenGet_ReturnsSignedDownloadUrl()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Health Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var created = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("doctor_note", "Referral", "See attached letter.", null, null)));
        var record = (await created.Content.ReadFromJsonAsync<HealthRecordResponse>())!;

        var uploadUrlResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/health-records/{record.Id}/attachment-upload-url", org.AccessToken,
            new CreateHealthRecordAttachmentUploadUrlRequest("application/pdf")));
        Assert.Equal(HttpStatusCode.OK, uploadUrlResponse.StatusCode);
        var uploadUrlBody = (await uploadUrlResponse.Content.ReadFromJsonAsync<CreateHealthRecordAttachmentUploadUrlResponse>())!;
        Assert.Contains("attachment.pdf", uploadUrlBody.UploadUrl);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/health-records", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<HealthRecordResponse>>())!;
        var updated = list.Single(r => r.Id == record.Id);
        Assert.NotNull(updated.AttachmentDownloadUrl);
    }

    [Fact]
    public async Task AttachmentUploadUrl_UnsupportedContentType_ReturnsUnprocessableEntity()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Health Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);

        var created = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("doctor_note", "Referral", "See attached letter.", null, null)));
        var record = (await created.Content.ReadFromJsonAsync<HealthRecordResponse>())!;

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{child.Id}/health-records/{record.Id}/attachment-upload-url", org.AccessToken,
            new CreateHealthRecordAttachmentUploadUrlRequest("application/zip")));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
