using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ChildCare.Contracts.Requests;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.StaffTimeEntries;

/// <summary>Feature 028/US2 (FR-009): a correction overlapping another entry for the same staff
/// member is a warning, not a block.</summary>
public class TimeEntryOverlapWarningTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<Guid> SeedEntryAsync(Guid tenantId, Guid staffProfileId, Guid locationId, DateTime clockedInAt, DateTime? clockedOutAt)
    {
        var schemaName = await GetSchemaNameAsync(factory.Services, tenantId);
        var db = ResolveTenantDb(factory.Services, schemaName);
        var entry = new Domain.Entities.StaffTimeEntry
        {
            StaffProfileId = staffProfileId,
            LocationId = locationId,
            ClockedInAt = clockedInAt,
            ClockedOutAt = clockedOutAt,
            Function = Domain.Enums.StaffTimeEntryFunction.Kinderbegeleider,
        };
        db.StaffTimeEntries.Add(entry);
        await db.SaveChangesAsync(CancellationToken.None);
        return entry.Id;
    }

    [Fact]
    public async Task Update_CreatingOverlap_SavesWithWarning_NotBlocked()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Overlap Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var staff = await CreateStaffAsync(client, org.AccessToken);
        await AssignEligibilityAsync(client, org.AccessToken, staff.Id, location.Id);

        var baseTime = DateTime.UtcNow.AddHours(-4);
        // Existing closed entry: 08:00-12:00 (relative).
        await SeedEntryAsync(org.Organisation.Id, staff.Id, location.Id, baseTime, baseTime.AddHours(4));
        // Second entry starts at 10:00, still open.
        var secondEntryId = await SeedEntryAsync(org.Organisation.Id, staff.Id, location.Id, baseTime.AddHours(2), null);

        // Correcting the second entry's clock-out to 14:00 overlaps the first entry (08:00-12:00
        // vs 10:00-14:00) — must save with a warning flag, not be rejected.
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Patch, $"/api/staff-time-entries/{secondEntryId}", org.AccessToken,
            new UpdateStaffTimeEntryRequest(baseTime.AddHours(6), null, null, null)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("overlapWarning").GetBoolean());
    }

    [Fact]
    public async Task Update_NoOverlap_NoWarning()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Overlap Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var staff = await CreateStaffAsync(client, org.AccessToken);
        await AssignEligibilityAsync(client, org.AccessToken, staff.Id, location.Id);

        var baseTime = DateTime.UtcNow.AddHours(-4);
        await SeedEntryAsync(org.Organisation.Id, staff.Id, location.Id, baseTime, baseTime.AddHours(2));
        var secondEntryId = await SeedEntryAsync(org.Organisation.Id, staff.Id, location.Id, baseTime.AddHours(3), null);

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Patch, $"/api/staff-time-entries/{secondEntryId}", org.AccessToken,
            new UpdateStaffTimeEntryRequest(baseTime.AddHours(4), null, null, null)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("overlapWarning").GetBoolean());
    }
}
