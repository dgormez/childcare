using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.Email;

/// <summary>User Story 1 (spec.md): director sends a one-off bulk email to a location or group.</summary>
public class BulkEmailEndpointsTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task AssignChildToGroupAsync(HttpClient client, string accessToken, Guid childId, Guid groupId) =>
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/groups", accessToken,
            new AssignChildToGroupRequest(groupId, new DateOnly(2023, 1, 1))));

    private static async Task<ContactResponse> CreateContactNoEmailAsync(HttpClient client, string accessToken, string firstName = "NoEmail") =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/contacts", accessToken,
            new CreateContactRequest(firstName, "Peeters", "+32 9 123 45 67", null, "nl"))))
            .Content.ReadFromJsonAsync<ContactResponse>())!;

    private static Task<int> GetRecipientCountAsync(HttpClient client, string accessToken, Guid locationId, Guid? groupId = null)
    {
        var query = groupId is null ? $"locationId={locationId}" : $"locationId={locationId}&groupId={groupId}";
        return client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/email/bulk-send/recipient-count?{query}", accessToken))
            .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<JsonRecipientCount>().Result.RecipientCount);
    }

    private record JsonRecipientCount(int RecipientCount);
    private record JsonBulkSendResult(Guid BulkEmailSendId, int SentCount, int SkippedNoEmailCount, int ProviderFailureCount);

    // ── FR-002: one email per household, not per child ──────────────────────────

    [Fact]
    public async Task SendBulkEmail_ContactLinkedToTwoChildren_ReceivesExactlyOneEmail()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"BulkEmail Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var childA = await CreateChildAsync(client, org.AccessToken, "Emma");
        var childB = await CreateChildAsync(client, org.AccessToken, "Liam");
        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, childA.Id, contact.Id);
        await LinkContactAsync(client, org.AccessToken, childB.Id, contact.Id);
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        await AssignChildToGroupAsync(client, org.AccessToken, childA.Id, group.Id);
        await AssignChildToGroupAsync(client, org.AccessToken, childB.Id, group.Id);

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/email/bulk-send", org.AccessToken,
            new SendBulkEmailRequest(location.Id, null, "Subject", "Body", null, null, null, null, null)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<JsonBulkSendResult>())!;
        Assert.Equal(1, result.SentCount);
        Assert.Single(fakeEmail.BulkEmailCalls, c => c.ToEmail == contact.Email);
    }

    // ── Cc/Bcc are applied to every individual recipient email in the batch ─────

    [Fact]
    public async Task SendBulkEmail_WithCcAndBcc_AppliesBothToEveryRecipientSend()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"BulkEmail Cc Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var child = await CreateChildAsync(client, org.AccessToken, "Emma");
        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, child.Id, contact.Id);
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id);

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/email/bulk-send", org.AccessToken,
            new SendBulkEmailRequest(location.Id, null, "Subject", "Body", null, null, null,
                Cc: ["co-director@test.com"], Bcc: ["archive@test.com"])));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var call = Assert.Single(fakeEmail.BulkEmailCalls, c => c.ToEmail == contact.Email);
        Assert.Equal(["co-director@test.com"], call.Cc);
        Assert.Equal(["archive@test.com"], call.Bcc);
    }

    [Fact]
    public async Task SendBulkEmail_WithInvalidCcAddress_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"BulkEmail Cc Invalid Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/email/bulk-send", org.AccessToken,
            new SendBulkEmailRequest(location.Id, null, "Subject", "Body", null, null, null,
                Cc: ["not-an-email"], Bcc: null)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.email.cc_invalid", await response.Content.ReadAsStringAsync());
    }

    // ── FR-012: a contact with no email is skipped, logged, doesn't block the batch ──

    [Fact]
    public async Task SendBulkEmail_ContactWithNoEmail_SkippedAndLogged()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"BulkEmailNoAddr {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var childNoEmail = await CreateChildAsync(client, org.AccessToken, "NoEmailChild");
        var contactNoEmail = await CreateContactNoEmailAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, childNoEmail.Id, contactNoEmail.Id);
        var childWithEmail = await CreateChildAsync(client, org.AccessToken, "WithEmailChild");
        var contactWithEmail = await CreateContactWithEmailAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, childWithEmail.Id, contactWithEmail.Id);
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        await AssignChildToGroupAsync(client, org.AccessToken, childNoEmail.Id, group.Id);
        await AssignChildToGroupAsync(client, org.AccessToken, childWithEmail.Id, group.Id);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/email/bulk-send", org.AccessToken,
            new SendBulkEmailRequest(location.Id, null, "Subject", "Body", null, null, null, null, null)));

        var result = (await response.Content.ReadFromJsonAsync<JsonBulkSendResult>())!;
        Assert.Equal(1, result.SentCount);
        Assert.Equal(1, result.SkippedNoEmailCount);
    }

    // ── FR-016: zero-recipient scope is a no-op, not an error ───────────────────

    [Fact]
    public async Task SendBulkEmail_ZeroRecipients_CompletesWithoutError()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"BulkEmailEmpty {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Empty Location");

        Assert.Equal(0, await GetRecipientCountAsync(client, org.AccessToken, location.Id));

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/email/bulk-send", org.AccessToken,
            new SendBulkEmailRequest(location.Id, null, "Subject", "Body", null, null, null, null, null)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<JsonBulkSendResult>())!;
        Assert.Equal(0, result.SentCount);
        Assert.Equal(0, result.SkippedNoEmailCount);
    }

    // ── FR-001: group scope narrows recipients to only that group ───────────────

    [Fact]
    public async Task SendBulkEmail_GroupScoped_ReachesOnlyThatGroup()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"BulkEmailGroup {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var groupA = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var groupB = await CreateGroupAsync(client, org.AccessToken, "Group B", location.Id);
        var childA = await CreateChildAsync(client, org.AccessToken, "ChildA");
        var contactA = await CreateContactWithEmailAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, childA.Id, contactA.Id);
        await AssignChildToGroupAsync(client, org.AccessToken, childA.Id, groupA.Id);
        var childB = await CreateChildAsync(client, org.AccessToken, "ChildB");
        var contactB = await CreateContactWithEmailAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, childB.Id, contactB.Id);
        await AssignChildToGroupAsync(client, org.AccessToken, childB.Id, groupB.Id);

        Assert.Equal(1, await GetRecipientCountAsync(client, org.AccessToken, location.Id, groupA.Id));

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/email/bulk-send", org.AccessToken,
            new SendBulkEmailRequest(location.Id, groupA.Id, "Subject", "Body", null, null, null, null, null)));

        var result = (await response.Content.ReadFromJsonAsync<JsonBulkSendResult>())!;
        Assert.Equal(1, result.SentCount);

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        Assert.Contains(fakeEmail.BulkEmailCalls, c => c.ToEmail == contactA.Email);
        Assert.DoesNotContain(fakeEmail.BulkEmailCalls, c => c.ToEmail == contactB.Email);
    }

    // ── FR-003: an attachment uploaded via signed URL is delivered intact ───────

    [Fact]
    public async Task SendBulkEmail_WithAttachment_DeliversAttachmentIntact()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"BulkEmailAttach {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var child = await CreateChildAsync(client, org.AccessToken);
        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, child.Id, contact.Id);
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id);

        var uploadUrlResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/email/attachments/upload-url", org.AccessToken,
            new BulkEmailAttachmentUploadUrlRequest("application/pdf")));
        Assert.Equal(HttpStatusCode.OK, uploadUrlResponse.StatusCode);
        var uploadResult = (await uploadUrlResponse.Content.ReadFromJsonAsync<JsonUploadUrlResult>())!;

        var fakeStorage = factory.Services.GetRequiredService<FakeBulkEmailAttachmentStorage>();
        fakeStorage.SeedObject(uploadResult.ObjectPath, [0x25, 0x50, 0x44, 0x46]); // "%PDF" magic bytes, tiny fake

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/email/bulk-send", org.AccessToken,
            new SendBulkEmailRequest(location.Id, null, "Subject", "Body", uploadResult.ObjectPath, "menu.pdf", "application/pdf", null, null)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<JsonBulkSendResult>())!;
        Assert.Equal(1, result.SentCount);

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        Assert.Contains(fakeEmail.BulkEmailCalls, c => c.ToEmail == contact.Email && c.HasAttachment);
    }

    private record JsonUploadUrlResult(string UploadUrl, string ObjectPath);

    // ── FR-012: a provider failure for one recipient doesn't block the rest ────

    [Fact]
    public async Task SendBulkEmail_OneRecipientProviderFailure_OthersStillSent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"BulkEmailFail {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);

        var childOk = await CreateChildAsync(client, org.AccessToken, "OkChild");
        var contactOk = await CreateContactWithEmailAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, childOk.Id, contactOk.Id);
        await AssignChildToGroupAsync(client, org.AccessToken, childOk.Id, group.Id);

        var childFail = await CreateChildAsync(client, org.AccessToken, "FailChild");
        var contactFail = await CreateContactWithEmailAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, childFail.Id, contactFail.Id);
        await AssignChildToGroupAsync(client, org.AccessToken, childFail.Id, group.Id);

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        fakeEmail.ThrowOnBulkEmailTo.Add(contactFail.Email!);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/email/bulk-send", org.AccessToken,
            new SendBulkEmailRequest(location.Id, null, "Subject", "Body", null, null, null, null, null)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<JsonBulkSendResult>())!;
        Assert.Equal(1, result.SentCount);
        Assert.Equal(1, result.ProviderFailureCount);
    }

    // ── FR-013: tenant isolation — cross-tenant location never targetable ───────

    [Fact]
    public async Task SendBulkEmail_CrossTenantLocation_Returns422()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"BulkEmailTenantA {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var orgB = await RegisterOrgAsync(client, $"BulkEmailTenantB {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");
        var locationB = await CreateLocationAsync(client, orgB.AccessToken, "Location B");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/email/bulk-send", orgA.AccessToken,
            new SendBulkEmailRequest(locationB.Id, null, "Subject", "Body", null, null, null, null, null)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
