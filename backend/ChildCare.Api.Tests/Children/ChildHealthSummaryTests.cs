using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.Children;

/// <summary>User Story 4 (spec.md FR-013/FR-015, research.md R3): an eligible caregiver sees a
/// child's active health records and due-soon vaccines via the health-summary endpoint; an
/// ineligible caller gets the same 404 a nonexistent child would.</summary>
public class ChildHealthSummaryTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static string ExtractLatestStaffInviteToken(OrganisationOnboardingWebAppFactory factory, string email)
    {
        var entry = factory.LogCapture.Entries.Last(e =>
            e.Message.Contains("Staff invitation link for", StringComparison.Ordinal) &&
            e.Message.Contains(email, StringComparison.Ordinal));
        var match = Regex.Match(entry.Message, @"token=([^&\s]+)");
        Assert.True(match.Success, $"No token found in log entry: {entry.Message}");
        return match.Groups[1].Value;
    }

    private static async Task<(StaffResponse Staff, string AccessToken)> CreateAndLoginCaregiverAsync(
        HttpClient client, OrganisationOnboardingWebAppFactory factory, string orgSlug, string directorAccessToken, params Guid[] locationIds)
    {
        var email = $"caregiver_{Guid.NewGuid():N}@test.com";
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", directorAccessToken,
            new CreateStaffProfileRequest("Care", "Giver", email, "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null)));
        var staff = (await createResponse.Content.ReadFromJsonAsync<StaffResponse>())!;

        foreach (var locationId in locationIds)
            await client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/staff/{staff.Id}/locations/{locationId}", directorAccessToken));

        var token = ExtractLatestStaffInviteToken(factory, email);
        await client.PostAsJsonAsync("/api/staff/accept-invitation", new AcceptStaffInvitationRequest(orgSlug, token, "password123"));

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = orgSlug, email, password = "password123" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var session = (await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        return (staff, session.AccessToken);
    }

    private static async Task AssignChildToGroupAsync(HttpClient client, string accessToken, Guid childId, Guid groupId) =>
        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/children/{childId}/groups", accessToken,
            new AssignChildToGroupRequest(groupId, new DateOnly(2026, 1, 1))));

    [Fact]
    public async Task HealthSummary_EligibleCaregiver_SeesActiveHealthRecordsAndDueSoonVaccines()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Health Summary Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room 1", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id);

        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("allergy", "Peanut allergy", "Confirmed by allergist.", null, null)));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", org.AccessToken,
            new CreateVaccineRecordRequest("DTP", null, today.AddDays(-10), today.AddDays(5), null, null)));

        var (_, caregiverToken) = await CreateAndLoginCaregiverAsync(client, factory, org.Organisation.Slug, org.AccessToken, location.Id);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/health-summary", caregiverToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content.ReadFromJsonAsync<ChildHealthSummaryResponse>())!;

        Assert.Single(summary.ActiveHealthRecords);
        Assert.Equal("Peanut allergy", summary.ActiveHealthRecords[0].Title);
        Assert.Single(summary.DueSoonVaccines);
        Assert.Equal("DTP", summary.DueSoonVaccines[0].VaccineName);
        Assert.False(summary.DueSoonVaccines[0].IsOverdue);
    }

    [Fact]
    public async Task HealthSummary_IneligibleCaregiver_ReturnsSameNotFoundAsNonexistentChild()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Health Summary Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var locationA = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var locationB = await CreateLocationAsync(client, org.AccessToken, "Location B");
        var groupB = await CreateGroupAsync(client, org.AccessToken, "Room B", locationB.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, groupB.Id);

        var (_, caregiverToken) = await CreateAndLoginCaregiverAsync(client, factory, org.Organisation.Slug, org.AccessToken, locationA.Id);

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/health-summary", caregiverToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.child.not_found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HealthSummary_AsDeviceToken_Succeeds()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Health Summary Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room 1", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await client.SendAsync(DeviceRequest(HttpMethod.Get, $"/api/children/{child.Id}/health-summary", deviceToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
