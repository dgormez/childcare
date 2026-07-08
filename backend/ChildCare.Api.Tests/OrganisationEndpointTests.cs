using System.Net;
using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests;

/// <summary>Feature 007a (spec.md FR-005a): the director web sidebar needs the organisation's
/// display name, which no existing endpoint returned (only TenantSlug is ever resolved, and
/// never surfaced to the client).</summary>
public class OrganisationEndpointTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task GetCurrentOrganisation_AsDirector_ReturnsTenantName()
    {
        var client = factory.CreateClient();
        var orgName = $"My Organisation {Guid.NewGuid():N}";
        var org = await RegisterOrgAsync(client, orgName, $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/organisations/me", org.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<OrganisationResponse>())!;
        Assert.Equal(orgName, body.Name);
    }

    [Fact]
    public async Task GetCurrentOrganisation_Unauthenticated_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/organisations/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetCurrentOrganisation_AsStaff_Returns403()
    {
        var client = factory.CreateClient();
        var email = $"staff_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Staff Forbidden Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.PublicDbContext>();
            var tenant = await publicDb.Tenants.SingleAsync(t => t.Id == org.Organisation.Id);
            var resolver = scope.ServiceProvider.GetRequiredService<ITenantDbContextResolver>();
            var db = resolver.ForSchema(tenant.SchemaName);
            db.Users.Add(new Domain.Entities.TenantUser
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Name = "Test Staff",
                Role = UserRole.Staff,
                EmailVerified = true,
            });
            await db.SaveChangesAsync();
        }

        var loginRes = await client.PostAsJsonAsync("/api/auth/login",
            new { organisationSlug = org.Organisation.Slug, email, password = "password123" });
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var staffToken = (await loginRes.Content.ReadFromJsonAsync<AuthSessionResponse>())!.AccessToken;

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/organisations/me", staffToken));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
