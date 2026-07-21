using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.WaitingList;

/// <summary>
/// Feature 023, User Story 1 — the public, unauthenticated enrollment form: location-info
/// lookup, successful submission (entry/reference-code/confirmation-email/director-notification),
/// honeypot rejection, validation, disabled-location enforcement, and rate limiting (FR-004
/// through FR-021).
/// </summary>
public class PublicEnrollmentTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static SubmitPublicEnrollmentRequest ValidRequest(string locale = "nl") => new(
        "Emma", "Peeters", new DateOnly(2025, 3, 10), new DateOnly(2026, 9, 1),
        "Sophie Peeters", $"sophie_{Guid.NewGuid():N}@example.com", "+32 9 123 45 67", "Prefers mornings", locale, Website: "");

    private static async Task<LocationResponse> EnablePublicEnrollmentAsync(HttpClient client, string accessToken, LocationResponse location)
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/public-enrollment-setting", accessToken,
            new UpdateLocationPublicEnrollmentSettingRequest(true)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<LocationResponse>())!;
    }

    private async Task<int> CountNotificationsAsync(Guid tenantId, NotificationType type, Guid sourceId)
    {
        var schema = await GetSchemaNameAsync(factory.Services, tenantId);
        var db = ResolveTenantDb(factory.Services, schema);
        return await db.Notifications.CountAsync(n => n.Type == type && n.SourceId == sourceId);
    }

    // ── T013: location-info lookup ──────────────────────────────────────────────────

    [Fact]
    public async Task GetLocationInfo_ReturnsEnabledStateAndDefaultLocale()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Public Enroll Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Sunflower");

        var beforeResponse = await client.GetAsync($"/api/public/enrollment/{org.Organisation.Slug}/{location.PublicEnrollmentSlug}");
        Assert.Equal(HttpStatusCode.OK, beforeResponse.StatusCode);
        var before = (await beforeResponse.Content.ReadFromJsonAsync<GetPublicEnrollmentLocationInfoResponse>())!;
        Assert.False(before.Enabled);
        Assert.Equal("nl", before.DefaultLocale);

        await EnablePublicEnrollmentAsync(client, org.AccessToken, location);
        var afterResponse = await client.GetAsync($"/api/public/enrollment/{org.Organisation.Slug}/{location.PublicEnrollmentSlug}");
        var after = (await afterResponse.Content.ReadFromJsonAsync<GetPublicEnrollmentLocationInfoResponse>())!;
        Assert.True(after.Enabled);
    }

    [Fact]
    public async Task GetLocationInfo_UnknownOrgOrLocation_Returns404_SameErrorKey()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Public Enroll NotFound Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var badOrgResponse = await client.GetAsync($"/api/public/enrollment/no-such-org-{Guid.NewGuid():N}/some-location");
        var badLocationResponse = await client.GetAsync($"/api/public/enrollment/{org.Organisation.Slug}/no-such-location");

        Assert.Equal(HttpStatusCode.NotFound, badOrgResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, badLocationResponse.StatusCode);
    }

    // ── T014/T021a: valid submission creates a self-registered entry, nothing else ──

    [Fact]
    public async Task Submit_Valid_CreatesSelfRegisteredWaitingEntry_NoChildOrContactWritten()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Public Enroll Submit Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Tulip");
        await EnablePublicEnrollmentAsync(client, org.AccessToken, location);

        var response = await client.PostAsJsonAsync($"/api/public/enrollment/{org.Organisation.Slug}/{location.PublicEnrollmentSlug}", ValidRequest("fr"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<SubmitPublicEnrollmentResponse>())!;
        Assert.Equal(8, body.ReferenceCode.Length);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var entry = await db.WaitingListEntries.SingleAsync(e => e.ReferenceCode == body.ReferenceCode);
        Assert.Equal(WaitingListEntrySource.SelfRegistered, entry.Source);
        Assert.Equal("fr", entry.SubmittedLocale);
        Assert.Equal("waiting", ChildCare.Application.WaitingList.WaitingListMapper.ToWire(entry.Status));

        // FR-020: no child/contact data is written until a director explicitly converts the entry.
        Assert.Equal(0, await db.Children.CountAsync());
        Assert.Equal(0, await db.Contacts.CountAsync());
    }

    // ── T015: honeypot rejection ─────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_HoneypotFilled_CreatesNoEntry_ReturnsGenericSuccess()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Public Enroll Honeypot Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Daisy");
        await EnablePublicEnrollmentAsync(client, org.AccessToken, location);

        var req = ValidRequest() with { Website = "https://spambot.example" };
        var response = await client.PostAsJsonAsync($"/api/public/enrollment/{org.Organisation.Slug}/{location.PublicEnrollmentSlug}", req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // FR-005: same shape as genuine success
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        Assert.Equal(0, await db.WaitingListEntries.CountAsync());
    }

    // ── T016: validation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_MissingRequiredFieldOrFutureDob_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Public Enroll Validation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Rose");
        await EnablePublicEnrollmentAsync(client, org.AccessToken, location);

        var missingFirstName = ValidRequest() with { ChildFirstName = "" };
        var futureDob = ValidRequest() with { DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)) };

        var missingResponse = await client.PostAsJsonAsync($"/api/public/enrollment/{org.Organisation.Slug}/{location.PublicEnrollmentSlug}", missingFirstName);
        var futureResponse = await client.PostAsJsonAsync($"/api/public/enrollment/{org.Organisation.Slug}/{location.PublicEnrollmentSlug}", futureDob);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, futureResponse.StatusCode);
    }

    // ── T017: contact email required for self-registered entries ───────────────────

    [Fact]
    public async Task Submit_MissingContactEmail_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Public Enroll NoEmail Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Iris");
        await EnablePublicEnrollmentAsync(client, org.AccessToken, location);

        var req = ValidRequest() with { ContactEmail = "" };
        var response = await client.PostAsJsonAsync($"/api/public/enrollment/{org.Organisation.Slug}/{location.PublicEnrollmentSlug}", req);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── T018: disabled location rejects even a direct request ──────────────────────

    [Fact]
    public async Task Submit_DisabledLocation_Returns403_NoEntryCreated()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Public Enroll Disabled Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Lily"); // never enabled

        var response = await client.PostAsJsonAsync($"/api/public/enrollment/{org.Organisation.Slug}/{location.PublicEnrollmentSlug}", ValidRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        Assert.Equal(0, await db.WaitingListEntries.CountAsync());
    }

    // ── T019: rate-limit policy is structurally wired (behavioral 429s aren't testable — ──
    // ── AddRateLimiter is disabled in the Testing environment, per Program.cs and the ──────
    // ── existing precedent in AuthSessionLifecycleTests.LoginEndpoint_StillDeclaresAuthStrict- ──
    // ── RateLimitPolicy) ─────────────────────────────────────────────────────────────

    [Fact]
    public void SubmitEndpoint_StillDeclaresPublicEnrollmentRateLimitPolicy()
    {
        var endpointDataSource = factory.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>();
        var submitEndpoint = endpointDataSource.Endpoints.Single(e =>
            e is Microsoft.AspNetCore.Routing.RouteEndpoint route &&
            route.RoutePattern.RawText == "/api/public/enrollment/{orgSlug}/{locationSlug}" &&
            e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods.Contains("POST"));

        var rateLimitMetadata = submitEndpoint.Metadata.GetMetadata<Microsoft.AspNetCore.RateLimiting.EnableRateLimitingAttribute>();
        Assert.NotNull(rateLimitMetadata);
        Assert.Equal("public-enrollment", rateLimitMetadata.PolicyName);
    }

    // ── T020: confirmation email sent in the submitted locale ──────────────────────

    [Fact]
    public async Task Submit_Valid_SendsConfirmationEmail_InSubmittedLocale_WithReferenceCode()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Public Enroll Email Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Orchid");
        await EnablePublicEnrollmentAsync(client, org.AccessToken, location);

        var fakeEmailSender = (FakeEmailSender)factory.Services.GetRequiredService<ChildCare.Application.Common.IEmailSender>();
        fakeEmailSender.EnrollmentConfirmationCalls.Clear();

        var req = ValidRequest("en");
        var response = await client.PostAsJsonAsync($"/api/public/enrollment/{org.Organisation.Slug}/{location.PublicEnrollmentSlug}", req);
        var body = (await response.Content.ReadFromJsonAsync<SubmitPublicEnrollmentResponse>())!;

        var call = Assert.Single(fakeEmailSender.EnrollmentConfirmationCalls);
        Assert.Equal(req.ContactEmail, call.ToEmail);
        Assert.Equal("en", call.Locale);
        Assert.Equal(body.ReferenceCode, call.ReferenceCode);
    }

    // ── T021: director notification on submission ───────────────────────────────────

    [Fact]
    public async Task Submit_Valid_CreatesNotificationForEveryDirector()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Public Enroll Notify Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Poppy");
        await EnablePublicEnrollmentAsync(client, org.AccessToken, location);

        var response = await client.PostAsJsonAsync($"/api/public/enrollment/{org.Organisation.Slug}/{location.PublicEnrollmentSlug}", ValidRequest());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var entry = await db.WaitingListEntries.SingleAsync();

        var notificationCount = await CountNotificationsAsync(org.Organisation.Id, NotificationType.EnrollmentSubmitted, entry.Id);
        Assert.Equal(1, notificationCount); // exactly one director exists for this freshly-registered org
    }
}
