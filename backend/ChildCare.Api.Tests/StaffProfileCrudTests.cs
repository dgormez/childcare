using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 1 (SC-001/SC-002/SC-003): a director creates a staff profile, the invitee
/// accepts and logs in. Also covers FR-006a/FR-006b (resend, single-use — /speckit-checklist
/// CHK004/CHK021), tenant isolation (FR-015), DirectorOnly enforcement, and the
/// /speckit-analyze G1/G2 follow-up test cases (login before accept, director opt-in without
/// qualification).
/// </summary>
public class StaffProfileCrudTests(OrganisationOnboardingWebAppFactory factory)
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

    private static async Task<HttpResponseMessage> LoginRawAsync(HttpClient client, string slug, string email, string password) =>
        await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = slug, email, password });

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return request;
    }

    private static CreateStaffProfileRequest DefaultCreateRequest(string email, string? qualification = "QualifiedCaregiver") =>
        new("Jane", "Doe", email, "+32 9 123 45 67", qualification, "Staff", null);

    /// <summary>
    /// Extracts the plaintext invite token from EmailService's dev-mode log line (SMTP isn't
    /// configured in tests) — the only place the plaintext token is ever observable, since
    /// StaffInvitation stores only its SHA256 hash (research.md R2). Takes the most recent
    /// matching entry so resend (which logs a second line for the same email) yields the new token.
    /// </summary>
    private static string ExtractLatestStaffInviteToken(OrganisationOnboardingWebAppFactory factory, string email)
    {
        var entry = factory.LogCapture.Entries.Last(e =>
            e.Message.Contains("Staff invitation link for", StringComparison.Ordinal) &&
            e.Message.Contains(email, StringComparison.Ordinal));
        var match = Regex.Match(entry.Message, @"token=([^&\s]+)");
        Assert.True(match.Success, $"No token found in log entry: {entry.Message}");
        return match.Groups[1].Value;
    }

    // ── T022: create with qualification → 201, invitation sent ──────────────────

    [Fact]
    public async Task CreateStaffProfile_WithQualification_ReturnsCreatedAndListsInOrganisation()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Staff CRUD Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff", org.AccessToken, DefaultCreateRequest(staffEmail)));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var staff = (await response.Content.ReadFromJsonAsync<StaffResponse>())!;
        Assert.Equal("Jane", staff.FirstName);
        Assert.Equal(staffEmail, staff.Email);
        Assert.Equal("QualifiedCaregiver", staff.QualificationLevel);
        Assert.Null(staff.DeactivatedAt);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<StaffResponse>>())!;
        Assert.Contains(list, s => s.Id == staff.Id);

        var token = ExtractLatestStaffInviteToken(factory, staffEmail);
        Assert.False(string.IsNullOrEmpty(token));
    }

    // ── T023: missing qualification for Staff role → 422 ─────────────────────────

    [Fact]
    public async Task CreateStaffProfile_MissingQualification_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Qual Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", org.AccessToken, DefaultCreateRequest($"staff_{Guid.NewGuid():N}@test.com", null)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.staff.qualification_required", await response.Content.ReadAsStringAsync());
    }

    // ── T024: accept invitation then log in ──────────────────────────────────────

    [Fact]
    public async Task AcceptInvitation_ThenLogin_Succeeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Accept Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";

        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff", org.AccessToken, DefaultCreateRequest(staffEmail)));
        var token = ExtractLatestStaffInviteToken(factory, staffEmail);

        var acceptResponse = await client.PostAsJsonAsync("/api/staff/accept-invitation",
            new AcceptStaffInvitationRequest(org.Organisation.Slug, token, "newpassword123"));
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var loginResponse = await LoginRawAsync(client, org.Organisation.Slug, staffEmail, "newpassword123");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    // ── T025: expired/unknown invitation token → 400 ─────────────────────────────

    [Fact]
    public async Task AcceptInvitation_UnknownToken_Returns400()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"BadToken Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.PostAsJsonAsync("/api/staff/accept-invitation",
            new AcceptStaffInvitationRequest(org.Organisation.Slug, "not-a-real-token", "newpassword123"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("errors.staff.invitation_invalid_or_expired", await response.Content.ReadAsStringAsync());
    }

    // ── T026/G2: director opt-in path — with and without qualification ───────────

    [Fact]
    public async Task CreateStaffProfile_DirectorOptIn_WithQualification_NoInvitationCreated()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"OptIn Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var schema = await GetSchemaNameAsync(org.Organisation.Id);

        var resolver = factory.Services.GetRequiredService<Application.Common.ITenantDbContextResolver>();
        var db = resolver.ForSchema(schema);
        var directorTenantUserId = org.Director.Id;

        var request = new CreateStaffProfileRequest(
            "Direct", "Or", "unused@test.com", "+32 9 000 00 00", "QualifiedCaregiver", "Director", directorTenantUserId);
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff", org.AccessToken, request));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var staff = (await response.Content.ReadFromJsonAsync<StaffResponse>())!;
        Assert.Equal(directorTenantUserId, staff.TenantUserId);
        Assert.Equal("Director", staff.Role);

        var userCountForDirector = await db.Users.CountAsync(u => u.Id == directorTenantUserId);
        Assert.Equal(1, userCountForDirector); // no second TenantUser row created

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<StaffResponse>>())!;
        Assert.Contains(list, s => s.Id == staff.Id);
    }

    [Fact]
    public async Task CreateStaffProfile_DirectorOptIn_QualificationOmitted_Returns201()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"OptInNoQual Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var request = new CreateStaffProfileRequest(
            "Direct", "Or", "unused@test.com", "+32 9 000 00 00", null, "Director", org.Director.Id);
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff", org.AccessToken, request));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var staff = (await response.Content.ReadFromJsonAsync<StaffResponse>())!;
        Assert.Null(staff.QualificationLevel);
    }

    // ── T027/T070: duplicate email — sequential and concurrent ──────────────────

    [Fact]
    public async Task CreateStaffProfile_DuplicateEmail_SecondAttemptReturns409()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Dup Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";

        var first = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff", org.AccessToken, DefaultCreateRequest(staffEmail)));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff", org.AccessToken, DefaultCreateRequest(staffEmail)));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("errors.staff.email_already_exists", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateStaffProfile_ConcurrentDuplicateEmail_ExactlyOneSucceeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Race Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";

        var tasks = Enumerable.Range(0, 2)
            .Select(_ => client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff", org.AccessToken, DefaultCreateRequest(staffEmail))))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    // ── T028: tenant isolation ────────────────────────────────────────────────────

    [Fact]
    public async Task StaffProfile_CreatedInOrgA_InvisibleToOrgB()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"Staff Isolation Org A {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var orgB = await RegisterOrgAsync(client, $"Staff Isolation Org B {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");

        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", orgA.AccessToken, DefaultCreateRequest($"staff_{Guid.NewGuid():N}@test.com")));
        var orgAStaff = (await createResponse.Content.ReadFromJsonAsync<StaffResponse>())!;

        var getAsOrgBResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/staff/{orgAStaff.Id}", orgB.AccessToken));
        Assert.Equal(HttpStatusCode.NotFound, getAsOrgBResponse.StatusCode);
        Assert.Contains("errors.staff.not_found", await getAsOrgBResponse.Content.ReadAsStringAsync());

        var listAsOrgBResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff", orgB.AccessToken));
        var listAsOrgB = (await listAsOrgBResponse.Content.ReadFromJsonAsync<List<StaffResponse>>())!;
        Assert.DoesNotContain(listAsOrgB, s => s.Id == orgAStaff.Id);
    }

    // ── T029: Staff/Parent roles get 403 on every staff endpoint ─────────────────

    [Fact]
    public async Task NonDirectorRoles_Get403OnAllStaffEndpoints()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Staff Role Enforcement Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var schema = await GetSchemaNameAsync(org.Organisation.Id);

        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";
        var parentEmail = $"parent_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(schema, staffEmail, "password123", UserRole.Staff);
        await InsertUserWithRoleAsync(schema, parentEmail, "password123", UserRole.Parent);

        var staffToken = await LoginAsync(client, org.Organisation.Slug, staffEmail, "password123");
        var parentToken = await LoginAsync(client, org.Organisation.Slug, parentEmail, "password123");

        foreach (var token in new[] { staffToken, parentToken })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff", token))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthedRequest(
                HttpMethod.Post, "/api/staff", token, DefaultCreateRequest($"x_{Guid.NewGuid():N}@test.com")))).StatusCode);
        }
    }

    // ── T066/FR-006a: resend invitation supersedes the old token ─────────────────

    [Fact]
    public async Task ResendInvitation_OldTokenRejected_NewTokenAccepted()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Resend Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";

        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff", org.AccessToken, DefaultCreateRequest(staffEmail)));
        var staff = (await createResponse.Content.ReadFromJsonAsync<StaffResponse>())!;
        var oldToken = ExtractLatestStaffInviteToken(factory, staffEmail);

        var resendResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff/{staff.Id}/resend-invitation", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, resendResponse.StatusCode);
        var newToken = ExtractLatestStaffInviteToken(factory, staffEmail);
        Assert.NotEqual(oldToken, newToken);

        var oldAcceptResponse = await client.PostAsJsonAsync("/api/staff/accept-invitation",
            new AcceptStaffInvitationRequest(org.Organisation.Slug, oldToken, "password123"));
        Assert.Equal(HttpStatusCode.BadRequest, oldAcceptResponse.StatusCode);

        var newAcceptResponse = await client.PostAsJsonAsync("/api/staff/accept-invitation",
            new AcceptStaffInvitationRequest(org.Organisation.Slug, newToken, "password123"));
        Assert.Equal(HttpStatusCode.OK, newAcceptResponse.StatusCode);
    }

    [Fact]
    public async Task ResendInvitation_AlreadyActiveAccount_Returns409()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ResendActive Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        // Director opt-in path — account already has credentials, no invitation ever created.
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", org.AccessToken,
            new CreateStaffProfileRequest("D", "O", "x@test.com", "+32 9 000 00 00", "QualifiedCaregiver", "Director", org.Director.Id)));
        var staff = (await createResponse.Content.ReadFromJsonAsync<StaffResponse>())!;

        var resendResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff/{staff.Id}/resend-invitation", org.AccessToken));
        Assert.Equal(HttpStatusCode.Conflict, resendResponse.StatusCode);
        Assert.Contains("errors.staff.account_already_active", await resendResponse.Content.ReadAsStringAsync());
    }

    // ── T067/FR-006b: accepting an invitation twice fails the second time ────────

    [Fact]
    public async Task AcceptInvitation_Twice_SecondAttemptFails()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"DoubleAccept Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";

        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff", org.AccessToken, DefaultCreateRequest(staffEmail)));
        var token = ExtractLatestStaffInviteToken(factory, staffEmail);

        var firstAccept = await client.PostAsJsonAsync("/api/staff/accept-invitation",
            new AcceptStaffInvitationRequest(org.Organisation.Slug, token, "password123"));
        Assert.Equal(HttpStatusCode.OK, firstAccept.StatusCode);

        var secondAccept = await client.PostAsJsonAsync("/api/staff/accept-invitation",
            new AcceptStaffInvitationRequest(org.Organisation.Slug, token, "differentpassword"));
        Assert.Equal(HttpStatusCode.BadRequest, secondAccept.StatusCode);
        Assert.Contains("errors.staff.invitation_invalid_or_expired", await secondAccept.Content.ReadAsStringAsync());
    }

    // ── T074/F1: invitation email send failure doesn't fail profile creation ────

    [Fact]
    public async Task CreateStaffProfile_InvitationEmailSendFails_ProfileStillCreated()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"EmailFailure Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var fakeEmailSender = factory.Services.GetRequiredService<FakeEmailSender>();
        fakeEmailSender.ThrowOnStaffInvitation = true;
        try
        {
            var response = await client.SendAsync(AuthedRequest(
                HttpMethod.Post, "/api/staff", org.AccessToken, DefaultCreateRequest($"staff_{Guid.NewGuid():N}@test.com")));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        finally
        {
            fakeEmailSender.ThrowOnStaffInvitation = false;
        }
    }

    // ── T068/G1: login before accepting the invitation fails cleanly ────────────

    [Fact]
    public async Task Login_BeforeAcceptingInvitation_FailsWithInvalidCredentialsNotServerError()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"PreAccept Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";

        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff", org.AccessToken, DefaultCreateRequest(staffEmail)));

        var loginResponse = await LoginRawAsync(client, org.Organisation.Slug, staffEmail, "anyPassword123");
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
        Assert.Contains("errors.auth.invalid_credentials", await loginResponse.Content.ReadAsStringAsync());
    }
}
