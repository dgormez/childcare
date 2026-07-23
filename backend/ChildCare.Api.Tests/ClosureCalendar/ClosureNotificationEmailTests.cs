using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.AttendanceTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.ClosureCalendar;

/// <summary>User Story 4 (spec.md FR-010): publishing a closure day additionally emails every
/// resolved contact with an email on file, alongside the existing push/in-app fan-out.</summary>
public class ClosureNotificationEmailTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly Monday = new(2027, 6, 7);

    private static Task<HttpResponseMessage> PublishClosureAsync(HttpClient client, string accessToken, Guid closureId, bool confirm = false, bool notifyParents = true) =>
        client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/closures/{closureId}/publish", accessToken, new PublishClosureDayRequest(confirm, notifyParents)));

    /// <summary>A contact with an email on file, linked as Mother/Father/Guardian to a child
    /// with an active Monday contract — resolvable by ClosureParentRecipientResolver. Never
    /// invited/accepted into the parent app, so TenantUserId stays null (the "no parent-app
    /// account" case the existing push-only fan-out could never reach).</summary>
    private static async Task<ContactResponse> CreateEnrolledChildWithEmailOnlyContactAsync(
        HttpClient client, string accessToken, Guid locationId, string firstName = "Emma")
    {
        var child = await CreateChildAsync(client, accessToken, firstName);
        await CreateAndActivateContractAsync(client, accessToken, child.Id, locationId, Monday.DayOfWeek);
        var contact = await CreateContactWithEmailAsync(client, accessToken);
        await LinkContactAsync(client, accessToken, child.Id, contact.Id);
        return contact;
    }

    [Fact]
    public async Task PublishClosure_EmailsContactWithNoParentAppAccount()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ClosureEmail Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var contact = await CreateEnrolledChildWithEmailOnlyContactAsync(client, org.AccessToken, location.Id);

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/closures", org.AccessToken,
            new CreateClosureDayRequest(location.Id, Monday, "Kerstvakantie", "holiday", true)));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var closure = (await createResponse.Content.ReadFromJsonAsync<ClosureDayResponse>())!;

        var publishResponse = await PublishClosureAsync(client, org.AccessToken, closure.Id);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        Assert.Contains(fakeEmail.ClosureNotificationEmailCalls, c => c.ToEmail == contact.Email);
    }

    [Fact]
    public async Task PublishClosure_EmailsDigestUnsubscribedContact()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ClosureEmailUnsub Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var contact = await CreateEnrolledChildWithEmailOnlyContactAsync(client, org.AccessToken, location.Id);

        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
            var tenant = publicDb.Tenants.Single(t => t.Slug == org.Organisation.Slug);
            var db = ResolveTenantDb(scope.ServiceProvider, tenant.SchemaName);
            var contactRow = await db.Contacts.SingleAsync(c => c.Id == contact.Id);
            contactRow.DigestUnsubscribedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/closures", org.AccessToken,
            new CreateClosureDayRequest(location.Id, Monday, "Kerstvakantie", "holiday", true)));
        var closure = (await createResponse.Content.ReadFromJsonAsync<ClosureDayResponse>())!;

        await PublishClosureAsync(client, org.AccessToken, closure.Id);

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        Assert.Contains(fakeEmail.ClosureNotificationEmailCalls, c => c.ToEmail == contact.Email);
    }

    // ── FR-012: a bad/bounced address doesn't block the rest of the batch ──────────

    [Fact]
    public async Task PublishClosure_OneRecipientEmailFailure_OthersStillEmailed()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ClosureEmailFail Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var okContact = await CreateEnrolledChildWithEmailOnlyContactAsync(client, org.AccessToken, location.Id, "OkChild");
        var failContact = await CreateEnrolledChildWithEmailOnlyContactAsync(client, org.AccessToken, location.Id, "FailChild");

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        fakeEmail.ThrowOnClosureNotificationEmailTo.Add(failContact.Email!);

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/closures", org.AccessToken,
            new CreateClosureDayRequest(location.Id, Monday, "Kerstvakantie", "holiday", true)));
        var closure = (await createResponse.Content.ReadFromJsonAsync<ClosureDayResponse>())!;

        var publishResponse = await PublishClosureAsync(client, org.AccessToken, closure.Id);

        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode); // the failure doesn't fail the whole request
        Assert.Contains(fakeEmail.ClosureNotificationEmailCalls, c => c.ToEmail == okContact.Email);
        Assert.Contains(fakeEmail.ClosureNotificationEmailCalls, c => c.ToEmail == failContact.Email); // attempted...
    }
}
