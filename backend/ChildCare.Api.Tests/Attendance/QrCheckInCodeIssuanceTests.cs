using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;
using static ChildCare.Api.Tests.AttendanceTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.Attendance;

/// <summary>Feature 021 — User Story 2, code issuance half (spec.md FR-005, research.md R3/R4).</summary>
public class QrCheckInCodeIssuanceTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<(HttpClient Client, RegisterOrganisationResponse Org, LocationResponse Location)> SetupEnabledLocationAsync(string orgLabel)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"{orgLabel} {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/qr-checkin-setting", org.AccessToken,
            new UpdateLocationQrCheckInSettingRequest(true)));
        return (client, org, location);
    }

    [Fact]
    public async Task Issue_ForLinkedChildAtEnabledLocation_Succeeds()
    {
        var (client, org, location) = await SetupEnabledLocationAsync("QR Issue Happy Org");
        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/parent/attendance/qr-code", parentToken, new IssueCheckInCodeRequest(child.Id)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<IssueCheckInCodeResponse>())!;
        Assert.NotEmpty(body.Code);
        Assert.Contains('.', body.Code);
    }

    // ── T020: a parent may never issue a code for a child they aren't linked to ─────

    [Fact]
    public async Task Issue_ForUnlinkedChild_Returns403NotYourChild()
    {
        var (client, org, location) = await SetupEnabledLocationAsync("QR Issue Unlinked Org");
        var otherChild = await CreateChildAsync(client, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, otherChild.Id, location.Id);

        // A parent linked to a *different* child, not otherChild.
        var (_, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/parent/attendance/qr-code", parentToken, new IssueCheckInCodeRequest(otherChild.Id)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("errors.qrCheckIn.not_your_child", await response.Content.ReadAsStringAsync());
    }

    // ── T021: issuance is rejected when the child's enrolled location has QR check-in disabled ──

    [Fact]
    public async Task Issue_LocationNotEnabled_Returns400NotEnabled()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"QR Issue Disabled Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        // Deliberately never enabling the setting for this location.

        var (child, _, parentToken) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await CreateAndActivateContractAsync(client, org.AccessToken, child.Id, location.Id);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/parent/attendance/qr-code", parentToken, new IssueCheckInCodeRequest(child.Id)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("errors.qrCheckIn.not_enabled", await response.Content.ReadAsStringAsync());
    }
}
