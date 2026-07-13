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

/// <summary>
/// User Story 1 (SC-001/SC-002/SC-003): a director creates a child file, with or without
/// medical information, independently of any contract. Also covers tenant isolation (FR-017),
/// DirectorOnly enforcement, the CHK001 future-date-of-birth fix, and child photo upload
/// (Phase 8, research.md R1).
/// </summary>
public class ChildCrudTests(OrganisationOnboardingWebAppFactory factory)
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

    private static CreateChildRequest MinimalCreateRequest(string firstName = "Emma", string lastName = "Peeters") =>
        new(firstName, lastName, new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null, null, null);

    private static CreateChildRequest FullMedicalCreateRequest() => new(
        "Louis", "Janssens", new DateOnly(2022, 1, 15),
        "Male", "Belgian",
        "Peanuts", "Severe",
        "Asthma", "No dairy",
        "Dr. Peeters", "+32 9 111 22 33",
        "Dr. Claes", "+32 9 444 55 66",
        "12345678901", null);

    // ── T030: create with core fields only ───────────────────────────────────────

    [Fact]
    public async Task CreateChild_WithCoreFieldsOnly_ReturnsCreatedAndListed()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Child CRUD Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/children", org.AccessToken, MinimalCreateRequest()));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var child = (await response.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.Equal("Emma", child.FirstName);
        Assert.Null(child.DeactivatedAt);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/children", org.AccessToken));
        var list = (await listResponse.Content.ReadFromJsonAsync<List<ChildResponse>>())!;
        Assert.Contains(list, c => c.Id == child.Id);
    }

    // ── T031: create with full medical info ──────────────────────────────────────

    [Fact]
    public async Task CreateChild_WithFullMedicalInformation_AllFieldsRetrievable()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Child Medical Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/children", org.AccessToken, FullMedicalCreateRequest()));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var child = (await response.Content.ReadFromJsonAsync<ChildResponse>())!;

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}", org.AccessToken));
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.Equal("Peanuts", reloaded.AllergiesDescription);
        Assert.Equal("Severe", reloaded.AllergySeverity);
        Assert.Equal("Asthma", reloaded.MedicalConditions);
        Assert.Equal("No dairy", reloaded.DietaryRestrictions);
        Assert.Equal("Dr. Peeters", reloaded.GpName);
        Assert.Equal("+32 9 111 22 33", reloaded.GpPhone);
        Assert.Equal("Dr. Claes", reloaded.PediatricianName);
        Assert.Equal("+32 9 444 55 66", reloaded.PediatricianPhone);
        Assert.Equal("12345678901", reloaded.HealthInsuranceNumber);
    }

    // ── T032: missing required fields → 422 ──────────────────────────────────────

    [Fact]
    public async Task CreateChild_MissingFirstName_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Child Validation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var request = MinimalCreateRequest(firstName: "");
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/children", org.AccessToken, request));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.child.firstname_required", await response.Content.ReadAsStringAsync());
    }

    // ── CHK001: date of birth in the future → 422 ────────────────────────────────

    [Fact]
    public async Task CreateChild_FutureDateOfBirth_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Child FutureDob Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var futureRequest = new CreateChildRequest(
            "Future", "Child", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            null, null, null, null, null, null, null, null, null, null, null, null);
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/children", org.AccessToken, futureRequest));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.child.date_of_birth_in_future", await response.Content.ReadAsStringAsync());
    }

    // ── T033: tenant isolation ────────────────────────────────────────────────────

    [Fact]
    public async Task Child_CreatedInOrgA_InvisibleToOrgB()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"Child Isolation Org A {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var orgB = await RegisterOrgAsync(client, $"Child Isolation Org B {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");

        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/children", orgA.AccessToken, MinimalCreateRequest()));
        var orgAChild = (await createResponse.Content.ReadFromJsonAsync<ChildResponse>())!;

        var getAsOrgBResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{orgAChild.Id}", orgB.AccessToken));
        Assert.Equal(HttpStatusCode.NotFound, getAsOrgBResponse.StatusCode);
        Assert.Contains("errors.child.not_found", await getAsOrgBResponse.Content.ReadAsStringAsync());

        var listAsOrgBResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/children", orgB.AccessToken));
        var listAsOrgB = (await listAsOrgBResponse.Content.ReadFromJsonAsync<List<ChildResponse>>())!;
        Assert.DoesNotContain(listAsOrgB, c => c.Id == orgAChild.Id);
    }

    // ── T034: Parent role gets 403 everywhere; Staff can read but not write ──────
    // (feature 008 opened GET /api/children/GET /api/children/{id} to StaffOrDirector so the
    // caregiver app can read them — write routes remain DirectorOnly, unchanged)

    [Fact]
    public async Task NonDirectorRoles_WriteEndpointsForbidden_ParentAlsoForbiddenOnReads()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Child Role Enforcement Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var schema = await GetSchemaNameAsync(org.Organisation.Id);

        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";
        var parentEmail = $"parent_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(schema, staffEmail, "password123", UserRole.Staff);
        await InsertUserWithRoleAsync(schema, parentEmail, "password123", UserRole.Parent);

        var staffToken = await LoginAsync(client, org.Organisation.Slug, staffEmail, "password123");
        var parentToken = await LoginAsync(client, org.Organisation.Slug, parentEmail, "password123");

        foreach (var token in new[] { staffToken, parentToken })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/children", token, MinimalCreateRequest()))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/children", parentToken))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/children", staffToken))).StatusCode);
    }

    // ── T084/T085: child photo upload (Phase 8, research.md R1) ─────────────────

    [Fact]
    public async Task RequestChildPhotoUploadUrl_ThenGet_ReturnsNonNullPhotoDownloadUrl()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Child Photo Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/children", org.AccessToken, MinimalCreateRequest()));
        var child = (await createResponse.Content.ReadFromJsonAsync<ChildResponse>())!;

        var uploadUrlResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/photo/upload-url", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, uploadUrlResponse.StatusCode);
        var uploadUrlBody = (await uploadUrlResponse.Content.ReadFromJsonAsync<RequestPhotoUploadUrlResponse>())!;
        Assert.Contains("children/", uploadUrlBody.ObjectPath);

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}", org.AccessToken));
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.False(string.IsNullOrEmpty(reloaded.PhotoDownloadUrl));
    }

    // ── 006a T018: pediatrician contact validation ───────────────────────────────

    [Fact]
    public async Task CreateChild_PediatricianNameTooLong_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Child PedName Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var request = MinimalCreateRequest() with { PediatricianName = new string('x', 201) };
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/children", org.AccessToken, request));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.child.pediatrician_name_too_long", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CreateChild_PediatricianPhoneTooLong_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Child PedPhone Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var request = MinimalCreateRequest() with { PediatricianPhone = new string('1', 31) };
        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/children", org.AccessToken, request));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("errors.child.pediatrician_phone_too_long", await response.Content.ReadAsStringAsync());
    }

    // ── 006a T025/T026: editing a child (US2) ────────────────────────────────────

    private static UpdateChildRequest ToUpdateRequest(ChildResponse c) => new(
        c.FirstName, c.LastName, c.DateOfBirth, c.Gender, c.Nationality,
        c.AllergiesDescription, c.AllergySeverity, c.MedicalConditions, c.DietaryRestrictions,
        c.GpName, c.GpPhone, c.PediatricianName, c.PediatricianPhone,
        c.HealthInsuranceNumber, c.Kindcode);

    [Fact]
    public async Task UpdateChild_PediatricianContact_PersistsIndependentlyOfGp()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Child Edit Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/children", org.AccessToken,
            MinimalCreateRequest() with { GpName = "Dr. Peeters", GpPhone = "+32 9 111 22 33" }));
        var child = (await createResponse.Content.ReadFromJsonAsync<ChildResponse>())!;

        var updateRequest = ToUpdateRequest(child) with { PediatricianName = "Dr. Claes", PediatricianPhone = "+32 9 444 55 66" };
        var updateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/children/{child.Id}", org.AccessToken, updateRequest));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var afterFirstEdit = (await updateResponse.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.Equal("Dr. Claes", afterFirstEdit.PediatricianName);
        Assert.Equal("+32 9 444 55 66", afterFirstEdit.PediatricianPhone);
        Assert.Equal("Dr. Peeters", afterFirstEdit.GpName);
        Assert.Equal("+32 9 111 22 33", afterFirstEdit.GpPhone);

        // Clearing the pediatrician contact must not touch the unrelated GP contact.
        var clearRequest = ToUpdateRequest(afterFirstEdit) with { PediatricianName = null, PediatricianPhone = null };
        var clearResponse = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/children/{child.Id}", org.AccessToken, clearRequest));
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);

        var afterClear = (await clearResponse.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.Null(afterClear.PediatricianName);
        Assert.Null(afterClear.PediatricianPhone);
        Assert.Equal("Dr. Peeters", afterClear.GpName);
        Assert.Equal("+32 9 111 22 33", afterClear.GpPhone);
    }

    [Fact]
    public async Task UpdateChild_ClearingRequiredField_Returns422AndDoesNotSave()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Child Edit Validation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/children", org.AccessToken, MinimalCreateRequest()));
        var child = (await createResponse.Content.ReadFromJsonAsync<ChildResponse>())!;

        var updateRequest = ToUpdateRequest(child) with { FirstName = "" };
        var updateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/children/{child.Id}", org.AccessToken, updateRequest));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, updateResponse.StatusCode);
        Assert.Contains("errors.child.firstname_required", await updateResponse.Content.ReadAsStringAsync());

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}", org.AccessToken));
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<ChildResponse>())!;
        Assert.Equal("Emma", reloaded.FirstName);
    }
}
