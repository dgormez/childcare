using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.Email;

/// <summary>User Story 3 (spec.md FR-009): on-demand daily-report resend, independent of
/// digest-unsubscribe state.</summary>
public class DailyReportResendEndpointTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private record JsonResendResult(int SentCount);

    [Fact]
    public async Task Resend_DigestUnsubscribedContact_StillReceivesEmail()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ResendUnsub Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken, "ResendChild");
        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, child.Id, contact.Id);

        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
            var tenant = publicDb.Tenants.Single(t => t.Slug == org.Organisation.Slug);
            var db = ResolveTenantDb(scope.ServiceProvider, tenant.SchemaName);
            var contactRow = await db.Contacts.SingleAsync(c => c.Id == contact.Id);
            contactRow.DigestUnsubscribedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/email/daily-report/{child.Id}/resend", org.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<JsonResendResult>())!;
        Assert.Equal(1, result.SentCount);

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        Assert.Contains(fakeEmail.DailyReportCalls, c => c.ToEmail == contact.Email);
    }

    [Fact]
    public async Task Resend_AvailableToDirector()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ResendDirector Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);
        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, child.Id, contact.Id);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/email/daily-report/{child.Id}/resend", org.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Resend_AvailableToStaff()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ResendStaff Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);
        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, child.Id, contact.Id);

        var staffEmail = $"caregiver_{Guid.NewGuid():N}@test.com";
        var createStaffResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", org.AccessToken,
            new CreateStaffProfileRequest("Care", "Giver", staffEmail, "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null)));
        Assert.Equal(HttpStatusCode.Created, createStaffResponse.StatusCode);

        var token = ExtractLatestStaffInviteToken(staffEmail);
        var acceptResponse = await client.PostAsJsonAsync("/api/staff/accept-invitation",
            new AcceptStaffInvitationRequest(org.Organisation.Slug, token, "password123"));
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new { organisationSlug = org.Organisation.Slug, email = staffEmail, password = "password123" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var session = (await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/email/daily-report/{child.Id}/resend", session.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private string ExtractLatestStaffInviteToken(string email)
    {
        var entry = factory.LogCapture.Entries.Last(e =>
            e.Message.Contains("Staff invitation link for", StringComparison.Ordinal) &&
            e.Message.Contains(email, StringComparison.Ordinal));
        var match = Regex.Match(entry.Message, @"token=([^&\s]+)");
        Assert.True(match.Success, $"No token found in log entry: {entry.Message}");
        return match.Groups[1].Value;
    }

    // ── FR-012: a bad/bounced address doesn't block the rest of this child's contacts ──

    [Fact]
    public async Task Resend_OneContactProviderFailure_OthersStillSent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ResendFail Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildAsync(client, org.AccessToken);
        var okContact = await CreateContactWithEmailAsync(client, org.AccessToken, firstName: "Ok");
        await LinkContactAsync(client, org.AccessToken, child.Id, okContact.Id, relationship: "Mother");
        var failContact = await CreateContactWithEmailAsync(client, org.AccessToken, firstName: "Fail");
        await LinkContactAsync(client, org.AccessToken, child.Id, failContact.Id, relationship: "Father");

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        fakeEmail.ThrowOnDailyReportTo.Add(failContact.Email!);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/email/daily-report/{child.Id}/resend", org.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // the failure doesn't fail the whole request
        var result = (await response.Content.ReadFromJsonAsync<JsonResendResult>())!;
        Assert.Equal(1, result.SentCount);
        Assert.Contains(fakeEmail.DailyReportCalls, c => c.ToEmail == okContact.Email);
        Assert.Contains(fakeEmail.DailyReportCalls, c => c.ToEmail == failContact.Email); // attempted...
    }

    [Fact]
    public async Task Resend_ChildIdOutsideCallerTenant_Returns404()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"ResendTenantA Org {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var orgB = await RegisterOrgAsync(client, $"ResendTenantB Org {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");
        var childB = await CreateChildAsync(client, orgB.AccessToken);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/email/daily-report/{childB.Id}/resend", orgA.AccessToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
