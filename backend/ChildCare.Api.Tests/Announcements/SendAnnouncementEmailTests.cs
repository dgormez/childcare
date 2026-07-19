using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.Announcements;

/// <summary>User Story 4 (spec.md FR-011): sending an announcement additionally emails every
/// resolved contact with an email on file, alongside the existing push/in-app fan-out.</summary>
public class SendAnnouncementEmailTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static Task AssignChildToGroupAsync(HttpClient client, string accessToken, Guid childId, Guid groupId) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/groups", accessToken,
            new AssignChildToGroupRequest(groupId, new DateOnly(2023, 1, 1))));

    [Fact]
    public async Task SendAnnouncement_EmailsContactWithNoParentAppAccount()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"AnnounceEmail Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        // No invite/accept — TenantUserId stays null, previously unreachable by the push/in-app fan-out.
        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, child.Id, contact.Id);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", org.AccessToken,
            new SendAnnouncementRequest(location.Id, null, "Closed Friday", "Staff training day")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var announcement = (await response.Content.ReadFromJsonAsync<AnnouncementResponse>())!;
        Assert.Equal(0, announcement.RecipientCount); // push/in-app reach unchanged (FR-008 boundary)

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        Assert.Contains(fakeEmail.AnnouncementEmailCalls, c => c.ToEmail == contact.Email && c.Subject == "Closed Friday" && c.Body == "Staff training day");
    }

    [Fact]
    public async Task SendAnnouncement_EmailsDigestUnsubscribedContact()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"AnnounceEmailUnsub Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, child.Id, contact.Id);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id);

        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
            var tenant = publicDb.Tenants.Single(t => t.Slug == org.Organisation.Slug);
            var db = ResolveTenantDb(scope.ServiceProvider, tenant.SchemaName);
            var contactRow = await db.Contacts.SingleAsync(c => c.Id == contact.Id);
            contactRow.DigestUnsubscribedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", org.AccessToken,
            new SendAnnouncementRequest(location.Id, null, "Subject", "Body")));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        Assert.Contains(fakeEmail.AnnouncementEmailCalls, c => c.ToEmail == contact.Email);
    }

    // ── FR-012: a bad/bounced address doesn't block the rest of the batch ──────────

    [Fact]
    public async Task SendAnnouncement_OneRecipientEmailFailure_OthersStillEmailed()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"AnnounceEmailFail Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
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
        fakeEmail.ThrowOnAnnouncementEmailTo.Add(contactFail.Email!);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/announcements", org.AccessToken,
            new SendAnnouncementRequest(location.Id, null, "Subject", "Body")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode); // the failure doesn't fail the whole request
        Assert.Contains(fakeEmail.AnnouncementEmailCalls, c => c.ToEmail == contactOk.Email);
        Assert.Contains(fakeEmail.AnnouncementEmailCalls, c => c.ToEmail == contactFail.Email);
    }
}
