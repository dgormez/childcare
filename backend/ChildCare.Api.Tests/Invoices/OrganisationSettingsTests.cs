using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.Invoices;

/// <summary>Feature 014 — spec.md User Story 1, PUT /api/organisations/me (first PUT on this resource).</summary>
public class OrganisationSettingsTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task PutMe_PersistsKboNumber()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"KBO Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, "/api/organisations/me", org.AccessToken, new UpdateOrganisationRequest("0123.456.789", null)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<OrganisationResponse>())!;
        Assert.Equal("0123.456.789", updated.KboNumber);

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/organisations/me", org.AccessToken));
        var fetched = (await getResponse.Content.ReadFromJsonAsync<OrganisationResponse>())!;
        Assert.Equal("0123.456.789", fetched.KboNumber);
    }

    [Fact]
    public async Task PutMe_AsNonDirector_Returns403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"KBO Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(schema, staffEmail, "password123", UserRole.Staff);
        var staffToken = await LoginAsync(client, org.Organisation.Slug, staffEmail, "password123");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, "/api/organisations/me", staffToken, new UpdateOrganisationRequest("0123.456.789", null)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
}
