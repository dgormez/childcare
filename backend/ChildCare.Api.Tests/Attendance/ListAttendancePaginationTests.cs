using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.AttendanceTestSupport;

namespace ChildCare.Api.Tests.Attendance;

/// <summary>User Story 4 (T047): director-web history view pagination via cursor.</summary>
public class ListAttendancePaginationTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly Monday = new(2026, 1, 5);

    [Fact]
    public async Task ListAttendance_Paginates_WithNoGapsOrOverlap()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"List Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var createdIds = new List<Guid>();
        for (var i = 0; i < 25; i++)
        {
            var child = await CreateChildAsync(client, org.AccessToken, $"Child{i}_{Guid.NewGuid():N}");
            var response = await CheckInChildAsync(client, deviceToken, child.Id, Monday);
            var record = (await response.Content.ReadFromJsonAsync<AttendanceRecordResponse>())!;
            createdIds.Add(record.Id);
        }

        var seenIds = new HashSet<Guid>();
        string? cursor = null;
        do
        {
            var response = await ListAttendanceAsync(client, org.AccessToken, location.Id, Monday, cursor, limit: 10);
            var page = (await response.Content.ReadFromJsonAsync<PagedAttendanceResponse>())!;
            Assert.True(page.Items.Count <= 10);
            foreach (var item in page.Items)
                Assert.True(seenIds.Add(item.Id), $"Duplicate item {item.Id} across pages");
            cursor = page.NextCursor;
        } while (cursor is not null);

        Assert.Equal(createdIds.Count, seenIds.Count);
        Assert.True(createdIds.All(seenIds.Contains));
    }
}
