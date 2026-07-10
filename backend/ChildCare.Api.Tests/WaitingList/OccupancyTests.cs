using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.WaitingList;

/// <summary>
/// Feature 012a, User Story 4 — projected occupancy: free capacity computed from active
/// contracts and Location.MaxCapacity (FR-014), closure days always reporting `closed: true`
/// with a null free-capacity count rather than a number (FR-015), and a regression proving the
/// computation never depends on AttendanceRecord (research.md R1, T047).
/// </summary>
public class OccupancyTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task<ChildResponse> CreateChildAsync(HttpClient client, string accessToken, string firstName = "Emma") =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/children", accessToken,
            new CreateChildRequest(firstName, "Peeters", new DateOnly(2023, 5, 10), null, null, null, null, null, null, null, null, null, null))))
            .Content.ReadFromJsonAsync<ChildResponse>())!;

    private static List<ContractedDayRequest> Days(params DayOfWeek[] weekdays) =>
        weekdays.Select(w => new ContractedDayRequest(w, new TimeOnly(8, 0), new TimeOnly(17, 0))).ToList();

    private static async Task<ContractResponse> CreateActiveContractAsync(
        HttpClient client, string accessToken, Guid childId, Guid locationId, DateOnly startDate, params DayOfWeek[] weekdays)
    {
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{childId}/contracts", accessToken,
            new CreateContractRequest(locationId, startDate, null, Days(weekdays), 3500, null)));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;

        var activateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", accessToken));
        Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
        return (await activateResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
    }

    private static async Task PublishClosureAsync(HttpClient client, string accessToken, Guid locationId, DateOnly date)
    {
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/closures", accessToken,
            new CreateClosureDayRequest(locationId, date, "Test closure", "holiday", false)));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var closure = (await createResponse.Content.ReadFromJsonAsync<ClosureDayResponse>())!;

        var publishResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/closures/{closure.Id}/publish", accessToken,
            new PublishClosureDayRequest(false)));
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
    }

    private static Task<HttpResponseMessage> OccupancyRawAsync(HttpClient client, string accessToken, Guid locationId, DateOnly from, DateOnly to) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/waiting-list/occupancy?locationId={locationId}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", accessToken));

    // ── T045: freeCapacity = MaxCapacity - active contracts covering that weekday/date ──────

    [Fact]
    public async Task Occupancy_ComputesFreeCapacityFromActiveContracts()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Occupancy Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main"); // MaxCapacity=20 (CreateLocationRequest helper default)
        var monday = new DateOnly(2026, 9, 7);
        var childA = await CreateChildAsync(client, org.AccessToken, "Emma");
        var childB = await CreateChildAsync(client, org.AccessToken, "Louis");
        await CreateActiveContractAsync(client, org.AccessToken, childA.Id, location.Id, monday, DayOfWeek.Monday);
        await CreateActiveContractAsync(client, org.AccessToken, childB.Id, location.Id, monday, DayOfWeek.Tuesday);

        var response = await OccupancyRawAsync(client, org.AccessToken, location.Id, monday, monday.AddDays(1));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var days = (await response.Content.ReadFromJsonAsync<List<OccupancyDayResponse>>())!;

        var mondayDay = days.Single(d => d.Date == monday);
        var tuesdayDay = days.Single(d => d.Date == monday.AddDays(1));
        Assert.Equal(location.MaxCapacity - 1, mondayDay.FreeCapacity);
        Assert.Equal(location.MaxCapacity - 1, tuesdayDay.FreeCapacity);
        Assert.False(mondayDay.Closed);
    }

    // ── T046: a published closure day is always closed:true, freeCapacity:null ─────────────

    [Fact]
    public async Task Occupancy_PublishedClosureDay_ReturnsClosedWithNullFreeCapacity()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Occupancy Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var closureDate = new DateOnly(2026, 9, 7);
        await PublishClosureAsync(client, org.AccessToken, location.Id, closureDate);

        var response = await OccupancyRawAsync(client, org.AccessToken, location.Id, closureDate, closureDate);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var days = (await response.Content.ReadFromJsonAsync<List<OccupancyDayResponse>>())!;

        var day = Assert.Single(days);
        Assert.True(day.Closed);
        Assert.Null(day.FreeCapacity);
    }

    // ── T047: never reads AttendanceRecord — a future date with zero attendance rows still ──
    // computes correctly (research.md R1).

    [Fact]
    public async Task Occupancy_FutureDateWithNoAttendanceRecords_StillComputesFromContracts()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Occupancy Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        // A fixed far-future Monday — CreateContractCommand only accepts weekdays Mon-Fri
        // (ContractedDayRequest), so this can't just be "+1 year" from an arbitrary today.
        var farFuture = new DateOnly(2028, 6, 5);
        var child = await CreateChildAsync(client, org.AccessToken);
        await CreateActiveContractAsync(client, org.AccessToken, child.Id, location.Id, farFuture, DayOfWeek.Monday);

        var response = await OccupancyRawAsync(client, org.AccessToken, location.Id, farFuture, farFuture);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var days = (await response.Content.ReadFromJsonAsync<List<OccupancyDayResponse>>())!;

        var day = Assert.Single(days);
        Assert.False(day.Closed);
        Assert.Equal(location.MaxCapacity - 1, day.FreeCapacity);
    }

    // ── Convergence T066: occupancy cannot be projected for a deactivated location ──────────
    // (spec.md Edge Cases).

    [Fact]
    public async Task Occupancy_ForDeactivatedLocation_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Occupancy Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var deactivateResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/locations/{location.Id}/deactivate", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        var response = await OccupancyRawAsync(client, org.AccessToken, location.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("errors.locations.not_found", await response.Content.ReadAsStringAsync());
    }
}
