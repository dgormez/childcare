using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>User Story 3: a director updates a staff member's phone/qualification and requests
/// a photo upload URL — photos are always served via signed URLs (FR-013).</summary>
public class StaffProfileUpdateTests(OrganisationOnboardingWebAppFactory factory)
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

    private async Task<string> GetSchemaNameAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.PublicDbContext>();
        var tenant = await publicDb.Tenants.SingleAsync(t => t.Id == tenantId);
        return tenant.SchemaName;
    }

    private async Task InsertUserWithRoleAsync(string schemaName, string email, string password, UserRole role)
    {
        var resolver = factory.Services.GetRequiredService<Application.Common.ITenantDbContextResolver>();
        var db = resolver.ForSchema(schemaName);
        db.Users.Add(new Domain.Entities.TenantUser
        {
            Email        = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Name         = $"Test {role}",
            Role         = role,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<string> LoginAsync(HttpClient client, string slug, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = slug, email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AuthSessionResponse>())!;
        return body.AccessToken;
    }

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    private static async Task<StaffResponse> CreateStaffAsync(HttpClient client, string accessToken) =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", accessToken,
            new CreateStaffProfileRequest("Jane", "Doe", $"staff_{Guid.NewGuid():N}@test.com", "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null))))
            .Content.ReadFromJsonAsync<StaffResponse>())!;

    // ── T045: update phone/qualification ─────────────────────────────────────────

    [Fact]
    public async Task UpdateStaffProfile_ChangesPhoneAndQualification_ReflectedOnReload()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Update Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staff = await CreateStaffAsync(client, org.AccessToken);

        var updateRequest = new UpdateStaffProfileRequest("Jane", "Doe", "+32 9 999 99 99", "Auxiliary");
        var updateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/staff/{staff.Id}", org.AccessToken, updateRequest));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/staff/{staff.Id}", org.AccessToken));
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<StaffResponse>())!;
        Assert.Equal("+32 9 999 99 99", reloaded.Phone);
        Assert.Equal("Auxiliary", reloaded.QualificationLevel);
    }

    // ── T046/T047: request upload URL, then GET reflects a photo URL ─────────────

    [Fact]
    public async Task RequestPhotoUploadUrl_ThenGet_ReturnsNonNullPhotoDownloadUrl()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Photo Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staff = await CreateStaffAsync(client, org.AccessToken);

        var uploadUrlResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff/{staff.Id}/photo/upload-url", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, uploadUrlResponse.StatusCode);
        var uploadUrlBody = (await uploadUrlResponse.Content.ReadFromJsonAsync<RequestPhotoUploadUrlResponse>())!;
        Assert.False(string.IsNullOrEmpty(uploadUrlBody.UploadUrl));
        Assert.False(string.IsNullOrEmpty(uploadUrlBody.ObjectPath));

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/staff/{staff.Id}", org.AccessToken));
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<StaffResponse>())!;
        Assert.False(string.IsNullOrEmpty(reloaded.PhotoDownloadUrl));
    }

    // ── T048: Staff-role token can't edit their own profile (director-only) ─────

    [Fact]
    public async Task StaffRole_CannotUpdateOwnProfile_Returns403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"SelfEdit Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staff = await CreateStaffAsync(client, org.AccessToken);

        var schema = await GetSchemaNameAsync(org.Organisation.Id);
        var staffLoginEmail = $"staffself_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(schema, staffLoginEmail, "password123", UserRole.Staff);
        var staffToken = await LoginAsync(client, org.Organisation.Slug, staffLoginEmail, "password123");

        var updateRequest = new UpdateStaffProfileRequest("Jane", "Doe", "+32 9 111 11 11", "Auxiliary");
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/staff/{staff.Id}", staffToken, updateRequest));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
