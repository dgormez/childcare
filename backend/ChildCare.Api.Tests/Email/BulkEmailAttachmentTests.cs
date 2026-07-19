using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.Email;

/// <summary>User Story 1 (spec.md FR-003/FR-017): bulk-email attachment content-type and
/// size-cap validation.</summary>
public class BulkEmailAttachmentTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private record JsonUploadUrlResult(string UploadUrl, string ObjectPath);
    private record JsonErrorBody(string ErrorKey);

    // ── FR-017: a disallowed content type is rejected before any upload URL is issued ──

    [Fact]
    public async Task UploadUrl_DisallowedContentType_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"AttachBadType Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/email/attachments/upload-url", org.AccessToken,
            new BulkEmailAttachmentUploadUrlRequest("application/zip")));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonErrorBody>())!;
        Assert.Equal("errors.email.invalid_content_type", body.ErrorKey);
    }

    // ── FR-017: an uploaded object over the 10MB cap is rejected at send time, not upload time ──
    // (a signed upload URL can't itself cap the byte count the client sends — research.md R3)

    [Fact]
    public async Task Send_AttachmentOverSizeCap_Returns422_NoEmailSent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"AttachTooBig Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var child = await CreateChildAsync(client, org.AccessToken);
        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, child.Id, contact.Id);
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/groups", org.AccessToken,
            new AssignChildToGroupRequest(group.Id, new DateOnly(2023, 1, 1))));

        var uploadUrlResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/email/attachments/upload-url", org.AccessToken,
            new BulkEmailAttachmentUploadUrlRequest("application/pdf")));
        Assert.Equal(HttpStatusCode.OK, uploadUrlResponse.StatusCode);
        var uploadResult = (await uploadUrlResponse.Content.ReadFromJsonAsync<JsonUploadUrlResult>())!;

        var fakeStorage = factory.Services.GetRequiredService<FakeBulkEmailAttachmentStorage>();
        fakeStorage.SeedObject(uploadResult.ObjectPath, new byte[10 * 1024 * 1024 + 1]); // one byte over the 10MB cap

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/email/bulk-send", org.AccessToken,
            new SendBulkEmailRequest(location.Id, null, "Subject", "Body", uploadResult.ObjectPath, "big.pdf", "application/pdf")));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonErrorBody>())!;
        Assert.Equal("errors.email.attachment_too_large", body.ErrorKey);

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        Assert.DoesNotContain(fakeEmail.BulkEmailCalls, c => c.ToEmail == contact.Email);
    }
}
