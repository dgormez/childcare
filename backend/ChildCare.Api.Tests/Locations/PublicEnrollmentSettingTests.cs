using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.Locations;

/// <summary>
/// Feature 023, User Story 4 — director enable/disable of public enrollment per location.
/// Mirrors LocationQrCheckInSettingTests' (021) isolation-test pattern exactly.
/// </summary>
public class PublicEnrollmentSettingTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    // ── T055: update persists and doesn't leak across locations ─────────────────────

    [Fact]
    public async Task UpdatePublicEnrollmentSetting_PersistsAndDoesNotAffectOtherLocations()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Multi Loc Enroll Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location1 = await CreateLocationAsync(client, org.AccessToken, "Location One");
        var location2 = await CreateLocationAsync(client, org.AccessToken, "Location Two");

        var updateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location1.Id}/public-enrollment-setting", org.AccessToken,
            new UpdateLocationPublicEnrollmentSettingRequest(true)));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = (await updateResponse.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.True(updated.PublicEnrollmentEnabled);

        var location2Reloaded = (await (await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{location2.Id}", org.AccessToken)))
            .Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.False(location2Reloaded.PublicEnrollmentEnabled);
    }

    [Fact]
    public async Task GetLocation_NeverConfigured_PublicEnrollmentDefaultsToFalse()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Enroll Defaults Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");

        var getResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/locations/{location.Id}", org.AccessToken));
        var reloaded = (await getResponse.Content.ReadFromJsonAsync<LocationResponse>())!;

        Assert.False(reloaded.PublicEnrollmentEnabled);
    }

    // ── T056: a submission attempted immediately after disabling is rejected server-side ──

    [Fact]
    public async Task DisableMidSession_SubmissionAttemptedRightAfter_IsRejected()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Enroll MidSession Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Marigold");

        await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/locations/{location.Id}/public-enrollment-setting", org.AccessToken,
            new UpdateLocationPublicEnrollmentSettingRequest(true)));
        await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/locations/{location.Id}/public-enrollment-setting", org.AccessToken,
            new UpdateLocationPublicEnrollmentSettingRequest(false)));

        var submitRequest = new SubmitPublicEnrollmentRequest(
            "Emma", "Peeters", new DateOnly(2025, 3, 10), null, "Sophie Peeters",
            $"sophie_{Guid.NewGuid():N}@example.com", null, null, "nl", Website: "");
        var response = await client.PostAsJsonAsync($"/api/public/enrollment/{org.Organisation.Slug}/{location.PublicEnrollmentSlug}", submitRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── T057: re-enabling leaves every previously submitted entry unchanged ─────────

    [Fact]
    public async Task ReEnable_LeavesExistingEntriesUnchanged()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Enroll Reenable Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Foxglove");

        await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/locations/{location.Id}/public-enrollment-setting", org.AccessToken,
            new UpdateLocationPublicEnrollmentSettingRequest(true)));

        var submitRequest = new SubmitPublicEnrollmentRequest(
            "Emma", "Peeters", new DateOnly(2025, 3, 10), null, "Sophie Peeters",
            $"sophie_{Guid.NewGuid():N}@example.com", null, null, "nl", Website: "");
        var submitResponse = await client.PostAsJsonAsync($"/api/public/enrollment/{org.Organisation.Slug}/{location.PublicEnrollmentSlug}", submitRequest);
        var reference = (await submitResponse.Content.ReadFromJsonAsync<SubmitPublicEnrollmentResponse>())!.ReferenceCode;

        await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/locations/{location.Id}/public-enrollment-setting", org.AccessToken,
            new UpdateLocationPublicEnrollmentSettingRequest(false)));
        await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/locations/{location.Id}/public-enrollment-setting", org.AccessToken,
            new UpdateLocationPublicEnrollmentSettingRequest(true)));

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/waiting-list?locationId={location.Id}", org.AccessToken));
        var entries = (await listResponse.Content.ReadFromJsonAsync<List<WaitingListEntryResponse>>())!;
        Assert.Contains(entries, e => e.ReferenceCode == reference);
    }
}
