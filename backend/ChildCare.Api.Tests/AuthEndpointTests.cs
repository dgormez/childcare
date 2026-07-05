using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// Rewritten for feature 003 (tasks.md T062): the old `/api/auth/register` endpoint this file
/// used to seed accounts through is deleted (research.md R10) — every test now seeds via
/// organisation onboarding instead. Scenarios already covered more thoroughly by
/// AuthMultiTenantLoginTests.cs (US1), AuthOAuthLinkOnlyTests.cs (US2), AuthRolePolicyTests.cs
/// (US3), and AuthSessionLifecycleTests.cs (US4) are not duplicated here — this file keeps only
/// the remaining scenarios (never-issued refresh token, account deletion, resend-verification)
/// those files don't already exercise.
/// </summary>
public class AuthEndpointTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task<CreateInvitationResponse> CreateInvitationAsync(HttpClient client, string email)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/invitations")
        {
            Content = JsonContent.Create(new CreateInvitationRequest(email)),
        };
        request.Headers.Add("X-Superadmin-Key", OrganisationOnboardingWebAppFactory.SuperAdminApiKey);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateInvitationResponse>())!;
    }

    private static async Task<RegisterOrganisationResponse> RegisterOrgAsync(HttpClient client, string orgName, string email)
    {
        var invitation = await CreateInvitationAsync(client, email);
        var response = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            invitation.Token, orgName, $"{orgName} Director", email, "password123"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RegisterOrganisationResponse>())!;
    }

    private static async Task<AuthSessionResponse> LoginAsync(HttpClient client, string slug, string email, string password = "password123")
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = slug, email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
    }

    // ── Register is gone (research.md R10, quickstart.md Scenario 6) ────────────

    [Fact]
    public async Task Register_Returns404_RouteNoLongerExists()
    {
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email = "anyone@example.com", password = "password123" });

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_TokenNeverIssued_Returns401()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Never Issued Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var res = await client.PostAsJsonAsync("/api/auth/refresh",
            new { organisationSlug = org.Organisation.Slug, refreshToken = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── Delete account ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAccount_Authenticated_Returns204AndPreventsFurtherLogin()
    {
        var client = factory.CreateClient();
        var email = $"delete_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Delete Account Org {Guid.NewGuid():N}", email);
        var auth = await LoginAsync(client, org.Organisation.Slug, email);

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/auth/account");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var deleteRes = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        var loginRes = await client.PostAsJsonAsync("/api/auth/login",
            new { organisationSlug = org.Organisation.Slug, email, password = "password123" });
        Assert.Equal(HttpStatusCode.Unauthorized, loginRes.StatusCode);
    }

    // ── Resend verification ──────────────────────────────────────────────────────

    [Fact]
    public async Task ResendVerification_Authenticated_Returns200()
    {
        var client = factory.CreateClient();
        var email = $"resend_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Resend Verification Org {Guid.NewGuid():N}", email);
        var auth = await LoginAsync(client, org.Organisation.Slug, email);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/resend-verification");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var res = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
