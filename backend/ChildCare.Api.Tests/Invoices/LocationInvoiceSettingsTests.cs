using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.Invoices;

/// <summary>Feature 014 — spec.md User Story 1, PUT /api/locations/{id}/invoice-settings.</summary>
public class LocationInvoiceSettingsTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task PutSettings_PersistsFields_AndLeavesOtherLocationsUnchanged()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Settings Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var otherLocation = await CreateLocationAsync(client, org.AccessToken, "Other");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/invoice-settings", org.AccessToken,
            new UpdateLocationInvoiceSettingsRequest("KDV-12345", "BE68539007547034", 21)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.Equal("KDV-12345", updated.Erkenningsnummer);
        Assert.Equal("BE68539007547034", updated.BankAccountNumber);
        Assert.Equal(21, updated.InvoiceDueDays);

        var otherResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{otherLocation.Id}", org.AccessToken));
        var other = (await otherResponse.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.Null(other.Erkenningsnummer);
        Assert.Null(other.BankAccountNumber);
        Assert.Equal(14, other.InvoiceDueDays);
    }

    [Fact]
    public async Task PutSettings_NegativeInvoiceDueDays_Returns422()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Settings Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/invoice-settings", org.AccessToken,
            new UpdateLocationInvoiceSettingsRequest(null, null, -1)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PutSettings_AsNonDirector_Returns403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Invoice Settings Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var staffEmail = $"staff_{Guid.NewGuid():N}@test.com";
        await InsertUserWithRoleAsync(schema, staffEmail, "password123", UserRole.Staff);
        var staffToken = await LoginAsync(client, org.Organisation.Slug, staffEmail, "password123");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/invoice-settings", staffToken,
            new UpdateLocationInvoiceSettingsRequest(null, null, 14)));

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
