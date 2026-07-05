using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 3 (SC-002): the three named authorization policies built in Foundational
/// (Program.cs, research.md R5) correctly gate access by role and fail closed (FR-013/FR-014)
/// — proven against TestSupportEndpoints.cs's three test-only, policy-guarded routes
/// (quickstart.md Scenario 3).
/// </summary>
public class AuthRolePolicyTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private const string DirectorOnlyRoute    = "/api/test-support/director-only";
    private const string StaffOrDirectorRoute = "/api/test-support/staff-or-director";
    private const string ParentOnlyRoute      = "/api/test-support/parent-only";

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
        var resolver = factory.Services.GetRequiredService<ITenantDbContextResolver>();
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

    private static async Task<HttpStatusCode> CallWithTokenAsync(HttpClient client, string route, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    /// <summary>Crafts a token directly (mirrors TenantRejectionTests.BuildToken) with a valid
    /// tenant_id claim but a caller-controlled (possibly missing/unrecognized) role claim, so
    /// FR-014's fail-closed guarantee can be tested independent of any real account.</summary>
    private static string BuildTokenWithRole(Guid tenantId, string? role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Email, "role-fail-closed@example.com"),
            new("tenant_id", tenantId.ToString()),
        };
        if (role is not null)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestWebAppFactoryBase.TestJwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer:             TestWebAppFactoryBase.TestJwtIssuer,
            audience:           TestWebAppFactoryBase.TestJwtAudience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(15),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<(HttpClient Client, string DirectorToken, string StaffToken, string ParentToken, Guid TenantId)> SeedOneAccountPerRoleAsync()
    {
        var client = factory.CreateClient();
        var directorEmail = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Role Policy Org {Guid.NewGuid():N}", directorEmail);
        var schema = await GetSchemaNameAsync(org.Organisation.Id);

        var staffEmail  = $"staff_{Guid.NewGuid():N}@test.com";
        var parentEmail = $"parent_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(schema, staffEmail, "password123", UserRole.Staff);
        await InsertUserWithRoleAsync(schema, parentEmail, "password123", UserRole.Parent);

        var directorToken = await LoginAsync(client, org.Organisation.Slug, directorEmail, "password123");
        var staffToken    = await LoginAsync(client, org.Organisation.Slug, staffEmail, "password123");
        var parentToken   = await LoginAsync(client, org.Organisation.Slug, parentEmail, "password123");

        return (client, directorToken, staffToken, parentToken, org.Organisation.Id);
    }

    // ── T042: DirectorOnly ───────────────────────────────────────────────────────

    [Fact]
    public async Task DirectorOnlyPolicy_AllowsDirector_RefusesStaffAndParent()
    {
        var (client, directorToken, staffToken, parentToken, _) = await SeedOneAccountPerRoleAsync();

        Assert.Equal(HttpStatusCode.OK, await CallWithTokenAsync(client, DirectorOnlyRoute, directorToken));
        Assert.Equal(HttpStatusCode.Forbidden, await CallWithTokenAsync(client, DirectorOnlyRoute, staffToken));
        Assert.Equal(HttpStatusCode.Forbidden, await CallWithTokenAsync(client, DirectorOnlyRoute, parentToken));
    }

    // ── T043: StaffOrDirector ────────────────────────────────────────────────────

    [Fact]
    public async Task StaffOrDirectorPolicy_AllowsStaffAndDirector_RefusesParent()
    {
        var (client, directorToken, staffToken, parentToken, _) = await SeedOneAccountPerRoleAsync();

        Assert.Equal(HttpStatusCode.OK, await CallWithTokenAsync(client, StaffOrDirectorRoute, directorToken));
        Assert.Equal(HttpStatusCode.OK, await CallWithTokenAsync(client, StaffOrDirectorRoute, staffToken));
        Assert.Equal(HttpStatusCode.Forbidden, await CallWithTokenAsync(client, StaffOrDirectorRoute, parentToken));
    }

    // ── T044: ParentOnly ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ParentOnlyPolicy_AllowsParentOnly_RefusesDirectorAndStaff()
    {
        var (client, directorToken, staffToken, parentToken, _) = await SeedOneAccountPerRoleAsync();

        Assert.Equal(HttpStatusCode.OK, await CallWithTokenAsync(client, ParentOnlyRoute, parentToken));
        Assert.Equal(HttpStatusCode.Forbidden, await CallWithTokenAsync(client, ParentOnlyRoute, directorToken));
        Assert.Equal(HttpStatusCode.Forbidden, await CallWithTokenAsync(client, ParentOnlyRoute, staffToken));
    }

    // ── T045: fail closed on a missing or unrecognized role claim (FR-014) ──────

    [Fact]
    public async Task AllPolicies_RefuseMissingOrUnrecognizedRoleClaim()
    {
        var (client, _, _, _, tenantId) = await SeedOneAccountPerRoleAsync();

        var noRoleToken = BuildTokenWithRole(tenantId, role: null);
        var unknownRoleToken = BuildTokenWithRole(tenantId, role: "superuser");

        foreach (var route in new[] { DirectorOnlyRoute, StaffOrDirectorRoute, ParentOnlyRoute })
        {
            Assert.Equal(HttpStatusCode.Forbidden, await CallWithTokenAsync(client, route, noRoleToken));
            Assert.Equal(HttpStatusCode.Forbidden, await CallWithTokenAsync(client, route, unknownRoleToken));
        }
    }
}
