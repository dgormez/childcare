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
/// User Story 1 (FR-001/FR-001a/FR-003/FR-004/FR-004a/FR-010): a director creates a draft
/// enrolment contract for a child at a location and activates it, subject to the
/// one-active-contract-per-location rule. Also covers tenant isolation (FR-015), DirectorOnly
/// enforcement (FR-014), and consent-defaulting (FR-010).
/// </summary>
public class ContractLifecycleTests(OrganisationOnboardingWebAppFactory factory)
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

    private static async Task<LocationResponse> CreateLocationAsync(HttpClient client, string accessToken, string name = "Main Building") =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/locations", accessToken,
            new CreateLocationRequest(name, "Address", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", 20))))
            .Content.ReadFromJsonAsync<LocationResponse>())!;

    private static async Task<ChildResponse> CreateChildAsync(HttpClient client, string accessToken) =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/children", accessToken,
            new CreateChildRequest("Emma", "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    private static List<ContractedDayRequest> Days(params DayOfWeek[] weekdays) =>
        weekdays.Select(w => new ContractedDayRequest(w, new TimeOnly(8, 0), new TimeOnly(17, 0))).ToList();

    private static CreateContractRequest MinimalCreateRequest(Guid locationId, params DayOfWeek[] weekdays) => new(
        locationId, new DateOnly(2026, 1, 1), null, Days(weekdays.Length == 0 ? [DayOfWeek.Monday] : weekdays), 3500, null);

    private static async Task<HttpResponseMessage> CreateContractAsync(HttpClient client, string accessToken, Guid childId, CreateContractRequest req) =>
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken, req));

    private static async Task<ContractResponse> CreateAndActivateAsync(HttpClient client, string accessToken, Guid childId, Guid locationId, params DayOfWeek[] weekdays)
    {
        var createResponse = await CreateContractAsync(client, accessToken, childId, MinimalCreateRequest(locationId, weekdays));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        var activateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", accessToken));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        return (await activateResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
    }

    // ── T023: minimal required fields ────────────────────────────────────────────

    [Fact]
    public async Task CreateContract_WithMinimalFields_ReturnsCreatedAsDraft()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Lifecycle Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var response = await CreateContractAsync(client, org.AccessToken, child.Id, MinimalCreateRequest(location.Id));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var contract = (await response.Content.ReadFromJsonAsync<ContractResponse>())!;
        Assert.Equal("draft", contract.Status);
        Assert.False(contract.Consent.PhotosInternal);
    }

    // ── T024: full optional fields round-trip ────────────────────────────────────

    [Fact]
    public async Task CreateContract_WithFullOptionalFields_AllFieldsRoundTrip()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Full Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var request = new CreateContractRequest(
            location.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            [
                new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0)),
                new ContractedDayRequest(DayOfWeek.Wednesday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
            ],
            4000,
            new ContractConsentRequest(true, false, true, false, false));

        var response = await CreateContractAsync(client, org.AccessToken, child.Id, request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ContractResponse>())!;

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{created.Id}", org.AccessToken));
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        Assert.Equal(2, reloaded.ContractedDays.Count);
        Assert.Equal(new DateOnly(2026, 12, 31), reloaded.EndDate);
        Assert.True(reloaded.Consent.PhotosInternal);
        Assert.True(reloaded.Consent.PhotosSocialMedia);
        Assert.False(reloaded.Consent.PhotosWebsite);
    }

    // ── T025: activate a draft ────────────────────────────────────────────────────

    [Fact]
    public async Task ActivateContract_OnDraft_ReturnsActive()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Activate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var contract = await CreateAndActivateAsync(client, org.AccessToken, child.Id, location.Id);
        Assert.Equal("active", contract.Status);
    }

    // ── T026: same-location double-activation rejected ───────────────────────────

    [Fact]
    public async Task ActivateContract_SecondAtSameLocation_ReturnsConflictAndFirstUnaffected()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract SameLoc Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var first = await CreateAndActivateAsync(client, org.AccessToken, child.Id, location.Id, DayOfWeek.Monday);

        var secondCreate = await CreateContractAsync(client, org.AccessToken, child.Id, MinimalCreateRequest(location.Id, DayOfWeek.Tuesday));
        var second = (await secondCreate.Content.ReadFromJsonAsync<ContractResponse>())!;
        var activateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{second.Id}/activate", org.AccessToken));

        Assert.Equal(HttpStatusCode.Conflict, activateResponse.StatusCode);
        Assert.Contains("errors.contract.already_active_at_location", await activateResponse.Content.ReadAsStringAsync());

        var firstReloaded = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{first.Id}", org.AccessToken));
        var firstContract = (await firstReloaded.Content.ReadFromJsonAsync<ContractResponse>())!;
        Assert.Equal("active", firstContract.Status);
    }

    // ── T027: field validation ────────────────────────────────────────────────────

    [Theory]
    [InlineData("empty_days")]
    [InlineData("weekend_day")]
    [InlineData("duplicate_weekday")]
    [InlineData("bad_time_range")]
    [InlineData("non_positive_rate")]
    [InlineData("end_before_start")]
    public async Task CreateContract_InvalidField_Returns422WithExpectedKey(string scenario)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Validation Org {Guid.NewGuid():N}_{scenario}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var (request, expectedKey) = scenario switch
        {
            "empty_days" => (new CreateContractRequest(location.Id, new DateOnly(2026, 1, 1), null, [], 3500, null),
                "errors.contract.weekday_required"),
            "weekend_day" => (new CreateContractRequest(location.Id, new DateOnly(2026, 1, 1), null, Days(DayOfWeek.Saturday), 3500, null),
                "errors.contract.weekday_invalid"),
            "duplicate_weekday" => (new CreateContractRequest(location.Id, new DateOnly(2026, 1, 1), null,
                [
                    new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)),
                    new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(13, 0), new TimeOnly(17, 0)),
                ], 3500, null),
                "errors.contract.weekday_invalid"),
            "bad_time_range" => (new CreateContractRequest(location.Id, new DateOnly(2026, 1, 1), null,
                [new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(17, 0), new TimeOnly(8, 0))], 3500, null),
                "errors.contract.time_range_invalid"),
            "non_positive_rate" => (new CreateContractRequest(location.Id, new DateOnly(2026, 1, 1), null, Days(DayOfWeek.Monday), 0, null),
                "errors.contract.daily_rate_invalid"),
            "end_before_start" => (new CreateContractRequest(location.Id, new DateOnly(2026, 1, 1), new DateOnly(2025, 12, 31), Days(DayOfWeek.Monday), 3500, null),
                "errors.contract.end_date_before_start_date"),
            _ => throw new InvalidOperationException(),
        };

        var response = await CreateContractAsync(client, org.AccessToken, child.Id, request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(expectedKey, await response.Content.ReadAsStringAsync());
    }

    // ── T027a: consent defaults to false when omitted ────────────────────────────

    [Fact]
    public async Task CreateContract_OmittingConsent_DefaultsEveryFlagToFalse()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Consent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var wholeObjectOmitted = new CreateContractRequest(location.Id, new DateOnly(2026, 1, 1), null, Days(DayOfWeek.Monday), 3500, null);
        var response1 = await CreateContractAsync(client, org.AccessToken, child.Id, wholeObjectOmitted);
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);
        var contract1 = (await response1.Content.ReadFromJsonAsync<ContractResponse>())!;
        Assert.False(contract1.Consent.PhotosInternal);
        Assert.False(contract1.Consent.PhotosWebsite);
        Assert.False(contract1.Consent.PhotosSocialMedia);
        Assert.False(contract1.Consent.VideoInternal);
        Assert.False(contract1.Consent.PhotosPress);

        var partialConsent = new CreateContractRequest(location.Id, new DateOnly(2026, 1, 1), null, Days(DayOfWeek.Tuesday), 3500,
            new ContractConsentRequest(true, false, false, false, false));
        var response2 = await CreateContractAsync(client, org.AccessToken, child.Id, partialConsent);
        var contract2 = (await response2.Content.ReadFromJsonAsync<ContractResponse>())!;
        Assert.True(contract2.Consent.PhotosInternal);
        Assert.False(contract2.Consent.PhotosWebsite);
    }

    // ── T027b: deactivated location rejected on create and on activate ──────────

    [Fact]
    public async Task CreateOrActivateContract_AtDeactivatedLocation_ReturnsLocationNotFound()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract DeactivatedLoc Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        // Draft created while the location is still active, so activation is the thing that
        // fails once the location is deactivated afterward.
        var createResponse = await CreateContractAsync(client, org.AccessToken, child.Id, MinimalCreateRequest(location.Id));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;

        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{location.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var activateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", org.AccessToken));
        Assert.Equal(HttpStatusCode.NotFound, activateResponse.StatusCode);
        Assert.Contains("errors.location.not_found", await activateResponse.Content.ReadAsStringAsync());

        var createAtDeactivatedResponse = await CreateContractAsync(client, org.AccessToken, child.Id, MinimalCreateRequest(location.Id));
        Assert.Equal(HttpStatusCode.NotFound, createAtDeactivatedResponse.StatusCode);
        Assert.Contains("errors.location.not_found", await createAtDeactivatedResponse.Content.ReadAsStringAsync());
    }

    // ── T028: draft edit in place; rejected once active ─────────────────────────

    [Fact]
    public async Task UpdateContract_WhileDraft_Succeeds_WhileActive_Rejected()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Update Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var createResponse = await CreateContractAsync(client, org.AccessToken, child.Id, MinimalCreateRequest(location.Id));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;

        var updateRequest = new UpdateContractRequest(new DateOnly(2026, 1, 1), null, Days(DayOfWeek.Monday), 5000, null);
        var updateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/contracts/{contract.Id}", org.AccessToken, updateRequest));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        Assert.Equal(5000, updated.DailyRateCents);

        var activateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

        var updateAfterActiveResponse = await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/contracts/{contract.Id}", org.AccessToken, updateRequest));
        Assert.Equal(HttpStatusCode.Conflict, updateAfterActiveResponse.StatusCode);
        Assert.Contains("errors.contract.not_draft", await updateAfterActiveResponse.Content.ReadAsStringAsync());
    }

    // ── T029: activate an already-active contract ────────────────────────────────

    [Fact]
    public async Task ActivateContract_AlreadyActive_ReturnsNotDraft()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract ReActivate Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);

        var contract = await CreateAndActivateAsync(client, org.AccessToken, child.Id, location.Id);

        var secondActivate = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", org.AccessToken));
        Assert.Equal(HttpStatusCode.Conflict, secondActivate.StatusCode);
        Assert.Contains("errors.contract.not_draft", await secondActivate.Content.ReadAsStringAsync());
    }

    // ── T030: tenant isolation ────────────────────────────────────────────────────

    [Fact]
    public async Task Contract_CreatedInOrgA_InvisibleToOrgB()
    {
        var client = factory.CreateClient();
        var orgA = await RegisterOrgAsync(client, $"Contract Isolation Org A {Guid.NewGuid():N}", $"director_a_{Guid.NewGuid():N}@test.com");
        var orgB = await RegisterOrgAsync(client, $"Contract Isolation Org B {Guid.NewGuid():N}", $"director_b_{Guid.NewGuid():N}@test.com");

        var locationA = await CreateLocationAsync(client, orgA.AccessToken);
        var childA = await CreateChildAsync(client, orgA.AccessToken);
        var createResponse = await CreateContractAsync(client, orgA.AccessToken, childA.Id, MinimalCreateRequest(locationA.Id));
        var contractA = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;

        var getAsOrgBResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/contracts/{contractA.Id}", orgB.AccessToken));
        Assert.Equal(HttpStatusCode.NotFound, getAsOrgBResponse.StatusCode);
        Assert.Contains("errors.contract.not_found", await getAsOrgBResponse.Content.ReadAsStringAsync());

        var listAsOrgBResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{childA.Id}/contracts", orgB.AccessToken));
        Assert.Equal(HttpStatusCode.NotFound, listAsOrgBResponse.StatusCode);
        Assert.Contains("errors.child.not_found", await listAsOrgBResponse.Content.ReadAsStringAsync());
    }

    // ── T031: non-Director roles get 403 ─────────────────────────────────────────

    [Fact]
    public async Task NonDirectorRoles_Get403OnContractEndpoints()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Role Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(org.Organisation.Id);

        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";
        var parentEmail = $"parent_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(schema, staffEmail, "password123", UserRole.Staff);
        await InsertUserWithRoleAsync(schema, parentEmail, "password123", UserRole.Parent);

        var staffToken = await LoginAsync(client, org.Organisation.Slug, staffEmail, "password123");
        var parentToken = await LoginAsync(client, org.Organisation.Slug, parentEmail, "password123");

        var createResponse = await CreateContractAsync(client, org.AccessToken, child.Id, MinimalCreateRequest(location.Id));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;

        foreach (var token in new[] { staffToken, parentToken })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthedRequest(
                HttpMethod.Post, $"/api/children/{child.Id}/contracts", token, MinimalCreateRequest(location.Id)))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthedRequest(
                HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", token))).StatusCode);
        }
    }

    // ── T067 (/speckit-converge F1): non-Director roles also get 403 on amend/terminate/pdf ──
    // ASP.NET Core's RequireAuthorization runs before the endpoint delegate, so a fake contract
    // id is enough — the 403 is returned before any handler/DB lookup ever executes (FR-014).

    [Fact]
    public async Task NonDirectorRoles_Get403OnAmendTerminateAndPdfEndpoints()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Contract Role Amend Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken);
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(org.Organisation.Id);

        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";
        var parentEmail = $"parent_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(schema, staffEmail, "password123", UserRole.Staff);
        await InsertUserWithRoleAsync(schema, parentEmail, "password123", UserRole.Parent);

        var staffToken = await LoginAsync(client, org.Organisation.Slug, staffEmail, "password123");
        var parentToken = await LoginAsync(client, org.Organisation.Slug, parentEmail, "password123");

        var contract = await CreateAndActivateAsync(client, org.AccessToken, child.Id, location.Id);
        var amendRequest = new AmendContractRequest(new DateOnly(2026, 6, 1), location.Id, null, Days(DayOfWeek.Monday), 4000, null);
        var terminateRequest = new TerminateContractRequest(new DateOnly(2026, 6, 30));

        foreach (var token in new[] { staffToken, parentToken })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthedRequest(
                HttpMethod.Post, $"/api/contracts/{contract.Id}/amend", token, amendRequest))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthedRequest(
                HttpMethod.Post, $"/api/contracts/{contract.Id}/terminate", token, terminateRequest))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(AuthedRequest(
                HttpMethod.Get, $"/api/contracts/{contract.Id}/pdf", token))).StatusCode);
        }
    }
}
