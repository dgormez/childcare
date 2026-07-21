using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.WaitingList;

/// <summary>
/// Feature 023, User Story 2, FR-014/SC-003 — converting a self-registered entry pre-fills the
/// child-profile creation flow with zero retyped fields. Reuses 012a's existing
/// `LinkChildToWaitingListEntryCommand(CreateNewChild: true)` path unchanged — this suite
/// confirms that pre-fill, built for director-entered entries, already produces identical
/// results for a self-registered one, since neither the command nor the entry shape it reads
/// from discriminates by `Source`. Contact-creation pre-fill (`LinkContactDialog`'s
/// `initialFirstName`/`initialLastName`/`initialPhone`/`initialEmail` props, wired from
/// `web/app/(app)/waiting-list/page.tsx`) is a web-layer concern with no backend API surface to
/// integration-test — verified by code review instead (tasks.md T038 note).
/// </summary>
public class PublicEnrollmentConversionTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static SubmitPublicEnrollmentRequest SelfRegisteredRequest() => new(
        "Amara", "Okafor", new DateOnly(2025, 1, 15), new DateOnly(2026, 9, 1), "Chidi Okafor",
        $"chidi_{Guid.NewGuid():N}@example.com", "+32 9 555 12 34", null, "en", Website: "");

    [Fact]
    public async Task ConvertSelfRegisteredEntry_ToEnrolled_CreateNewChild_PreFillsNameAndDateOfBirth()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Conversion Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Hyacinth");
        await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/public-enrollment-setting", org.AccessToken,
            new UpdateLocationPublicEnrollmentSettingRequest(true)));

        var submitResponse = await client.PostAsJsonAsync(
            $"/api/public/enrollment/{org.Organisation.Slug}/{location.PublicEnrollmentSlug}", SelfRegisteredRequest());
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        var listResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/waiting-list?locationId={location.Id}", org.AccessToken));
        var entry = Assert.Single((await listResponse.Content.ReadFromJsonAsync<List<WaitingListEntryResponse>>())!);
        Assert.Equal("selfRegistered", entry.Source);

        var offerResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/waiting-list/{entry.Id}/status", org.AccessToken, new TransitionWaitingListStatusRequest("offered")));
        Assert.Equal(HttpStatusCode.OK, offerResponse.StatusCode);
        var enrollResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/waiting-list/{entry.Id}/status", org.AccessToken, new TransitionWaitingListStatusRequest("enrolled")));
        Assert.Equal(HttpStatusCode.OK, enrollResponse.StatusCode);

        var linkResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/waiting-list/{entry.Id}/link-child", org.AccessToken,
            new LinkChildToWaitingListEntryRequest(null, true)));
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);
        var linkedEntry = (await linkResponse.Content.ReadFromJsonAsync<WaitingListEntryResponse>())!;
        Assert.NotNull(linkedEntry.ChildId);

        var childResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{linkedEntry.ChildId}", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, childResponse.StatusCode);
        var child = (await childResponse.Content.ReadFromJsonAsync<ChildResponse>())!;

        Assert.Equal("Amara", child.FirstName);
        Assert.Equal("Okafor", child.LastName);
        Assert.Equal(new DateOnly(2025, 1, 15), child.DateOfBirth);
    }
}
