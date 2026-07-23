using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.StaffTimeEntries;

/// <summary>
/// Feature 028/US1 (FR-001/FR-001a/FR-002/FR-003/FR-004/FR-004a/FR-005/FR-005a/FR-010): staff
/// clock in/out, with the eligibility/function-configuration integrity checks a subsidy-hours
/// record depends on.
/// </summary>
public class ClockInOutTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<(StaffResponse Staff, string AccessToken)> CreateAndLoginStaffAsync(
        HttpClient client, string orgSlug, string directorAccessToken, Guid locationId, string firstName = "Care")
    {
        var email = $"staff_{Guid.NewGuid():N}@test.com";
        var createResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff", directorAccessToken,
            new CreateStaffProfileRequest(firstName, "Giver", email, "+32 9 123 45 67", "QualifiedCaregiver", "Staff", null)));
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

    private static async Task SetFunctionsAsync(HttpClient client, string directorAccessToken, Guid staffId, params string[] functions)
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Patch, $"/api/staff/{staffId}/time-entry-functions", directorAccessToken,
            new UpdateStaffTimeEntryFunctionsRequest(functions)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ClockIn_SingleConfiguredFunction_AutoSelectsIt_NoFunctionRequired()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Time Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (staff, staffToken) = await CreateAndLoginStaffAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);
        await SetFunctionsAsync(client, org.AccessToken, staff.Id, "kinderbegeleider");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff-time-entries/clock-in", staffToken,
            new ClockInRequest(location.Id, null, null)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entry = (await response.Content.ReadFromJsonAsync<StaffTimeEntryResponse>())!;
        Assert.Equal("kinderbegeleider", entry.Function);
        Assert.True(entry.IsOpen);
    }

    [Fact]
    public async Task ClockIn_MultipleConfiguredFunctions_RequiresFunction_ThenSucceedsWithOne()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Time Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (staff, staffToken) = await CreateAndLoginStaffAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);
        await SetFunctionsAsync(client, org.AccessToken, staff.Id, "kinderbegeleider", "logistiek");

        var withoutFunction = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff-time-entries/clock-in", staffToken,
            new ClockInRequest(location.Id, null, null)));
        Assert.Equal(HttpStatusCode.BadRequest, withoutFunction.StatusCode);

        var withFunction = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff-time-entries/clock-in", staffToken,
            new ClockInRequest(location.Id, null, "logistiek")));
        Assert.Equal(HttpStatusCode.OK, withFunction.StatusCode);
        var entry = (await withFunction.Content.ReadFromJsonAsync<StaffTimeEntryResponse>())!;
        Assert.Equal("logistiek", entry.Function);
    }

    [Fact]
    public async Task ClockIn_NoFunctionConfigured_Rejected()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Time Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (_, staffToken) = await CreateAndLoginStaffAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff-time-entries/clock-in", staffToken,
            new ClockInRequest(location.Id, null, null)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ClockIn_WhileAlreadyOpen_Rejected()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Time Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (staff, staffToken) = await CreateAndLoginStaffAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);
        await SetFunctionsAsync(client, org.AccessToken, staff.Id, "kinderbegeleider");

        var first = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-time-entries/clock-in", staffToken, new ClockInRequest(location.Id, null, null)));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-time-entries/clock-in", staffToken, new ClockInRequest(location.Id, null, null)));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task ClockOut_ClosesOpenEntry_AndReturns404WhenNoneOpen()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Time Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (staff, staffToken) = await CreateAndLoginStaffAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);
        await SetFunctionsAsync(client, org.AccessToken, staff.Id, "kinderbegeleider");

        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-time-entries/clock-in", staffToken, new ClockInRequest(location.Id, null, null)));

        var clockOut = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-time-entries/clock-out", staffToken));
        Assert.Equal(HttpStatusCode.OK, clockOut.StatusCode);
        var entry = (await clockOut.Content.ReadFromJsonAsync<StaffTimeEntryResponse>())!;
        Assert.False(entry.IsOpen);
        Assert.NotNull(entry.ClockedOutAt);

        var secondClockOut = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/staff-time-entries/clock-out", staffToken));
        Assert.Equal(HttpStatusCode.NotFound, secondClockOut.StatusCode);
    }

    [Fact]
    public async Task ClockIn_LocationNotEligible_Rejected()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Time Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var eligibleLocation = await CreateLocationAsync(client, org.AccessToken, "Eligible");
        var otherLocation = await CreateLocationAsync(client, org.AccessToken, "Other");
        var (staff, staffToken) = await CreateAndLoginStaffAsync(client, org.Organisation.Slug, org.AccessToken, eligibleLocation.Id);
        await SetFunctionsAsync(client, org.AccessToken, staff.Id, "kinderbegeleider");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff-time-entries/clock-in", staffToken,
            new ClockInRequest(otherLocation.Id, null, null)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ClockIn_FunctionNotConfiguredForStaff_RejectedEvenIfNotOfferedByClient()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Time Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var (staff, staffToken) = await CreateAndLoginStaffAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);
        await SetFunctionsAsync(client, org.AccessToken, staff.Id, "kinderbegeleider");

        // "verantwoordelijke" was never configured for this staff member and never offered by
        // any client UI — the server must reject it independently (FR-005a).
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff-time-entries/clock-in", staffToken,
            new ClockInRequest(location.Id, null, "verantwoordelijke")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ClockIn_GroupNotBelongingToLocation_Rejected()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Time Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var otherLocation = await CreateLocationAsync(client, org.AccessToken, "Other");
        var otherGroup = await CreateGroupAsync(client, org.AccessToken, "Other Group", otherLocation.Id);
        var (staff, staffToken) = await CreateAndLoginStaffAsync(client, org.Organisation.Slug, org.AccessToken, location.Id);
        await SetFunctionsAsync(client, org.AccessToken, staff.Id, "kinderbegeleider");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/staff-time-entries/clock-in", staffToken,
            new ClockInRequest(location.Id, otherGroup.Id, null)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
