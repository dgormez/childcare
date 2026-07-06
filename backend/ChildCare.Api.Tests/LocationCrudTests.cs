using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// User Story 1 (SC-001): a director creates, lists, views, and updates locations scoped to
/// their own organisation. Also covers FR-007/SC-004 (tenant isolation), FR-011 (DirectorOnly
/// enforcement — /speckit-analyze finding G1), FR-014 (no KBO field — finding G3), and FR-017
/// (last-write-wins concurrency — finding G4), all folded into this file per tasks.md T011-T016.
/// </summary>
public class LocationCrudTests(OrganisationOnboardingWebAppFactory factory)
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

    private static CreateLocationRequest DefaultCreateRequest(string name = "Main Building") =>
        new(name, "123 Kerkstraat, 9000 Gent", "+32 9 123 45 67", $"{Guid.NewGuid():N}@location.test", 20);

    private static UpdateLocationRequest ToUpdateRequest(LocationResponse l, string? name = null) => new(
        name ?? l.Name, l.Address, l.Phone, l.Email, l.MaxCapacity,
        l.NaamLocatie, l.Dossiernummer, l.Verantwoordelijke, l.FlexPermission, l.BoPermission);

    // ── T011: create with core fields → 201, defaults, no KBO field ─────────────

    [Fact]
    public async Task CreateLocation_WithCoreFields_ReturnsDefaultsAndNoKboField()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"CRUD Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var createRequest = DefaultCreateRequest();
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, createRequest));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            Assert.DoesNotContain("kbo", prop.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ondernemingsnummer", prop.Name, StringComparison.OrdinalIgnoreCase);
        }

        var location = (await response.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.Equal(createRequest.Name, location.Name);
        Assert.Equal(createRequest.Address, location.Address);
        Assert.Equal(createRequest.Phone, location.Phone);
        Assert.Equal(createRequest.Email, location.Email);
        Assert.Equal(createRequest.MaxCapacity, location.MaxCapacity);
        Assert.Null(location.NaamLocatie);
        Assert.Null(location.Dossiernummer);
        Assert.Null(location.Verantwoordelijke);
        Assert.False(location.FlexPermission);
        Assert.False(location.BoPermission);
        Assert.Null(location.DeactivatedAt);
    }

    // ── T012: second location, independent list + edit ──────────────────────────

    [Fact]
    public async Task CreateSecondLocation_ListsBothIndependently_EditingOneDoesNotAffectOther()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Multi Loc Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var createResponse1 = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, DefaultCreateRequest("Location One")));
        var location1 = (await createResponse1.Content.ReadFromJsonAsync<LocationResponse>())!;

        var createResponse2 = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, DefaultCreateRequest("Location Two")));
        var location2 = (await createResponse2.Content.ReadFromJsonAsync<LocationResponse>())!;

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/locations", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<LocationResponse>>())!;
        Assert.Contains(list, l => l.Id == location1.Id);
        Assert.Contains(list, l => l.Id == location2.Id);

        var updateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location1.Id}", org.AccessToken, ToUpdateRequest(location1, "Location One Renamed")));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var getLocation2Response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{location2.Id}", org.AccessToken));
        var location2Reloaded = (await getLocation2Response.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.Equal("Location Two", location2Reloaded.Name);
    }

    // ── T013/T043/T044: validation failures — required fields, formats, lengths ─

    [Theory]
    [InlineData("", "Address", "+32 9 123 45 67", "email@test.com", 10, "errors.location.name_required")]
    [InlineData("Name", "", "+32 9 123 45 67", "email@test.com", 10, "errors.location.address_required")]
    [InlineData("Name", "Address", "", "email@test.com", 10, "errors.location.phone_required")]
    [InlineData("Name", "Address", "+32 9 123 45 67", "", 10, "errors.location.email_required")]
    [InlineData("Name", "Address", "+32 9 123 45 67", "not-an-email", 10, "errors.location.email_invalid")]
    [InlineData("Name", "Address", "abc-not-a-phone", "email@test.com", 10, "errors.location.phone_invalid")]
    [InlineData("Name", "Address", "+32 9 123 45 67", "email@test.com", 0, "errors.location.max_capacity_invalid")]
    [InlineData("Name", "Address", "+32 9 123 45 67", "email@test.com", -5, "errors.location.max_capacity_invalid")]
    public async Task CreateLocation_InvalidField_Returns422WithFieldError(
        string name, string address, string phone, string email, int maxCapacity, string expectedKey)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Validation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/locations", org.AccessToken,
            new CreateLocationRequest(name, address, phone, email, maxCapacity)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedKey, body);
    }

    // ── T044: an over-length field returns a field-specific 422, not a raw DB error ──

    [Fact]
    public async Task CreateLocation_NameExceedsMaxLength_Returns422NotDatabaseError()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"MaxLength Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var tooLongName = new string('a', 201);
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/locations", org.AccessToken,
            new CreateLocationRequest(tooLongName, "Address", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", 10)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.location.name_too_long", await response.Content.ReadAsStringAsync());
    }

    // ── T014: tenant isolation — a location in Org A is invisible to Org B ──────

    [Fact]
    public async Task Location_CreatedInOrgA_InvisibleToOrgB()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"Isolation Loc Org A {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var orgB = await RegisterOrgAsync(client, $"Isolation Loc Org B {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");

        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", orgA.AccessToken, DefaultCreateRequest()));
        var orgALocation = (await createResponse.Content.ReadFromJsonAsync<LocationResponse>())!;

        var getAsOrgBResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{orgALocation.Id}", orgB.AccessToken));
        Assert.Equal(HttpStatusCode.NotFound, getAsOrgBResponse.StatusCode);
        Assert.Contains("errors.location.not_found", await getAsOrgBResponse.Content.ReadAsStringAsync());

        var listAsOrgBResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/locations", orgB.AccessToken));
        var listAsOrgB = (await listAsOrgBResponse.Content.ReadFromJsonAsync<List<LocationResponse>>())!;
        Assert.DoesNotContain(listAsOrgB, l => l.Id == orgALocation.Id);
    }

    // ── T015: Staff/Parent roles get 403 on every location route (FR-011) ───────

    [Fact]
    public async Task NonDirectorRoles_Get403OnAllLocationEndpoints()
    {
        var client = factory.CreateClient();
        var directorEmail = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Role Enforcement Org {Guid.NewGuid():N}", directorEmail);
        var schema = await GetSchemaNameAsync(org.Organisation.Id);

        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";
        var parentEmail = $"parent_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(schema, staffEmail, "password123", UserRole.Staff);
        await InsertUserWithRoleAsync(schema, parentEmail, "password123", UserRole.Parent);

        var staffToken = await LoginAsync(client, org.Organisation.Slug, staffEmail, "password123");
        var parentToken = await LoginAsync(client, org.Organisation.Slug, parentEmail, "password123");

        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, DefaultCreateRequest()));
        var location = (await createResponse.Content.ReadFromJsonAsync<LocationResponse>())!;

        foreach (var token in new[] { staffToken, parentToken })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/locations", token))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{location.Id}", token))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", token, DefaultCreateRequest()))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/locations/{location.Id}", token, ToUpdateRequest(location)))).StatusCode);
        }
    }

    // ── T016: concurrent edits resolve last-write-wins (FR-017) ─────────────────

    [Fact]
    public async Task ConcurrentUpdates_ResolveLastWriteWins()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Concurrency Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/locations", org.AccessToken, DefaultCreateRequest()));
        var location = (await createResponse.Content.ReadFromJsonAsync<LocationResponse>())!;

        var firstUpdate = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}", org.AccessToken, ToUpdateRequest(location, "First Write")));
        Assert.Equal(HttpStatusCode.OK, firstUpdate.StatusCode);

        var secondUpdate = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}", org.AccessToken, ToUpdateRequest(location, "Second Write")));
        Assert.Equal(HttpStatusCode.OK, secondUpdate.StatusCode);

        var finalGet = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{location.Id}", org.AccessToken));
        var final = (await finalGet.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.Equal("Second Write", final.Name);
    }
}
