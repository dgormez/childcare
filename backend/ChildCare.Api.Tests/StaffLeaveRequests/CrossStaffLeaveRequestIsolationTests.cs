using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.StaffLeaveRequests;

/// <summary>Feature 027/US4 (FR-012/FR-015): GetMyLeaveRequestsQuery never returns another staff
/// member's requests.</summary>
public class CrossStaffLeaveRequestIsolationTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly FutureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(14);

    private async Task<(StaffResponse Staff, string AccessToken)> CreateAndLoginCaregiverAsync(
        HttpClient client, string orgSlug, string directorAccessToken, Guid locationId)
    {
        var email = $"caregiver_{Guid.NewGuid():N}@test.com";
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", directorAccessToken,
            new CreateStaffProfileRequest("Care", "Giver", email, "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null)));
        var staff = (await createResponse.Content.ReadFromJsonAsync<StaffResponse>())!;
        await AssignEligibilityAsync(client, directorAccessToken, staff.Id, locationId);

        var entry = factory.LogCapture.Entries.Last(e =>
            e.Message.Contains("Staff invitation link for", StringComparison.Ordinal) &&
            e.Message.Contains(email, StringComparison.Ordinal));
        var match = System.Text.RegularExpressions.Regex.Match(entry.Message, @"token=([^&\s]+)");
        var token = match.Groups[1].Value;

        await client.PostAsJsonAsync("/api/staff/accept-invitation", new AcceptStaffInvitationRequest(orgSlug, token, "password123"));
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { organisationSlug = orgSlug, email, password = "password123" });
        var session = (await loginResponse.Content.ReadFromJsonAsync<AuthSessionResponse>())!;

        return (staff, session.AccessToken);
    }

    [Fact]
    public async Task GetMyLeaveRequests_TwoStaffMembers_EachSeesOnlyTheirOwn()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Leave Isolation Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (_, tokenA) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);
        var (_, tokenB) = await CreateAndLoginCaregiverAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);

        var createA = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-leave-requests", tokenA,
            new CreateLeaveRequestRequest("annual", FutureDate, FutureDate.AddDays(1), "A's request")));
        var requestA = (await createA.Content.ReadFromJsonAsync<StaffLeaveRequestResponse>())!;

        var createB = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-leave-requests", tokenB,
            new CreateLeaveRequestRequest("other", FutureDate, FutureDate.AddDays(1), "B's request")));
        var requestB = (await createB.Content.ReadFromJsonAsync<StaffLeaveRequestResponse>())!;

        var meA = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff-leave-requests/me", tokenA));
        var entriesA = (await meA.Content.ReadFromJsonAsync<List<StaffLeaveRequestResponse>>())!;
        Assert.Single(entriesA);
        Assert.Equal(requestA.Id, entriesA[0].Id);

        var meB = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff-leave-requests/me", tokenB));
        var entriesB = (await meB.Content.ReadFromJsonAsync<List<StaffLeaveRequestResponse>>())!;
        Assert.Single(entriesB);
        Assert.Equal(requestB.Id, entriesB[0].Id);

        // Director queue sees both.
        var queueResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/staff-leave-requests", org.AccessToken));
        var queue = (await queueResponse.Content.ReadFromJsonAsync<List<StaffLeaveRequestResponse>>())!;
        Assert.Contains(queue, r => r.Id == requestA.Id);
        Assert.Contains(queue, r => r.Id == requestB.Id);
    }
}
