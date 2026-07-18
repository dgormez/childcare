using System.Net;
using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.Reporting;

/// <summary>User Story 2 (spec.md FR-005): on-demand BKR breach-window reconstruction for a
/// historical date range (research.md R3), and the default-range/empty-state/validation
/// behavior.</summary>
public class BkrBreachHistoryEndpointsTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static Task<HttpResponseMessage> GetBreachesAsync(HttpClient client, string accessToken, string query) =>
        client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/reports/bkr/breaches{query}", accessToken));

    [Fact]
    public async Task BreachHistory_ReconstructsKnownBreachWindow_FromSeededTimestamps()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Breach Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var staff = await CreateStaffAsync(client, org.AccessToken, "Anna");
        await AssignEligibilityAsync(client, org.AccessToken, staff.Id, location.Id);
        var (devicePairingId, _) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var yesterday = BelgianCalendarDay.Today().AddDays(-1);
        var (dayStartUtc, _) = BelgianCalendarDay.UtcRangeFor(yesterday);
        var checkInAt = dayStartUtc.AddHours(9);
        var checkOutAt = dayStartUtc.AddHours(17);

        var schemaName = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schemaName);

        for (var i = 0; i < 9; i++)
        {
            var child = await CreateChildAsync(client, org.AccessToken, $"BreachChild{i}_{Guid.NewGuid():N}");
            db.AttendanceRecords.Add(new AttendanceRecord
            {
                ChildId = child.Id, LocationId = location.Id, Date = yesterday,
                Status = AttendanceStatus.Present, CheckInAt = checkInAt, CheckOutAt = checkOutAt,
            });
            db.ChildGroupAssignments.Add(new ChildGroupAssignment
            {
                ChildId = child.Id, GroupId = group.Id, StartDate = yesterday, EndDate = yesterday,
            });
        }

        db.RoomShifts.Add(new RoomShift
        {
            StaffProfileId = staff.Id, LocationId = location.Id, GroupId = group.Id,
            DevicePairingId = devicePairingId, CheckedInAt = checkInAt, CheckedOutAt = checkOutAt,
        });

        await db.SaveChangesAsync();

        var response = await GetBreachesAsync(client, org.AccessToken, $"?from={yesterday:yyyy-MM-dd}&to={yesterday:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<BkrBreachHistoryResponse>())!;

        var breach = Assert.Single(body.Breaches, b => b.GroupId == group.Id);
        Assert.Equal(checkInAt, breach.StartedAt);
        Assert.Equal(checkOutAt, breach.EndedAt);
    }

    [Fact]
    public async Task BreachHistory_NoBreachesInRange_ReturnsEmptyArray()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Breach Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);

        var response = await GetBreachesAsync(client, org.AccessToken, "");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<BkrBreachHistoryResponse>())!;
        Assert.Empty(body.Breaches);

        // Default range (no from/to) is the last 30 days.
        Assert.Equal(30, body.To.DayNumber - body.From.DayNumber);
    }

    [Fact]
    public async Task BreachHistory_RangeExceeding366Days_ReturnsValidationError()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Breach Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var today = BelgianCalendarDay.Today();

        var response = await GetBreachesAsync(client, org.AccessToken, $"?from={today.AddDays(-400):yyyy-MM-dd}&to={today:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }
}
