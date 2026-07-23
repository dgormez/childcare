using System.Net;
using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.StaffTimeEntries;

/// <summary>
/// Feature 028/US2 (FR-006/FR-007/FR-007a/FR-008/FR-005a): the 7-day computed lock, the
/// director unlock/re-lock override (attributable — FR-007a), and the configured-function
/// constraint applied to a correction the same way it applies to clock-in.
/// </summary>
public class TimeEntryLockTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task SetFunctionsAsync(HttpClient client, string directorAccessToken, Guid staffId, params string[] functions)
    {
        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Patch, $"/api/staff/{staffId}/time-entry-functions", directorAccessToken,
            new UpdateStaffTimeEntryFunctionsRequest(functions)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<Guid> SeedEntryAsync(Guid tenantId, Guid staffProfileId, Guid locationId, DateTime clockedInAt)
    {
        var schemaName = await GetSchemaNameAsync(factory.Services, tenantId);
        var db = ResolveTenantDb(factory.Services, schemaName);
        var entry = new Domain.Entities.StaffTimeEntry
        {
            StaffProfileId = staffProfileId,
            LocationId = locationId,
            ClockedInAt = clockedInAt,
            Function = Domain.Enums.StaffTimeEntryFunction.Kinderbegeleider,
        };
        db.StaffTimeEntries.Add(entry);
        await db.SaveChangesAsync(CancellationToken.None);
        return entry.Id;
    }

    [Fact]
    public async Task Update_WithinLockWindow_Succeeds_AndPastLockWindow_IsRejected()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Lock Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var staff = await CreateStaffAsync(client, org.AccessToken);
        await AssignEligibilityAsync(client, org.AccessToken, staff.Id, location.Id);
        await SetFunctionsAsync(client, org.AccessToken, staff.Id, "kinderbegeleider");

        var freshEntryId = await SeedEntryAsync(org.Organisation.Id, staff.Id, location.Id, DateTime.UtcNow.AddHours(-1));
        var clockOutAt = DateTime.UtcNow;
        var freshUpdate = await client.SendAsync(AuthedRequest(
            HttpMethod.Patch, $"/api/staff-time-entries/{freshEntryId}", org.AccessToken,
            new UpdateStaffTimeEntryRequest(clockOutAt, null, null, null)));
        Assert.Equal(HttpStatusCode.OK, freshUpdate.StatusCode);

        var oldEntryId = await SeedEntryAsync(org.Organisation.Id, staff.Id, location.Id, DateTime.UtcNow.AddDays(-8));
        var oldUpdate = await client.SendAsync(AuthedRequest(
            HttpMethod.Patch, $"/api/staff-time-entries/{oldEntryId}", org.AccessToken,
            new UpdateStaffTimeEntryRequest(DateTime.UtcNow.AddDays(-8).AddHours(8), null, null, null)));
        Assert.Equal((HttpStatusCode)423, oldUpdate.StatusCode);
    }

    [Fact]
    public async Task Unlock_SetsUnlockedByDirector_StaysUnlockedUntilExplicitRelock()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Lock Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var staff = await CreateStaffAsync(client, org.AccessToken);
        await AssignEligibilityAsync(client, org.AccessToken, staff.Id, location.Id);

        var entryId = await SeedEntryAsync(org.Organisation.Id, staff.Id, location.Id, DateTime.UtcNow.AddDays(-8));

        var unlock = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff-time-entries/{entryId}/unlock", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, unlock.StatusCode);
        var unlockedEntry = (await unlock.Content.ReadFromJsonAsync<StaffTimeEntryResponse>())!;
        Assert.False(unlockedEntry.IsLocked);
        Assert.NotNull(unlockedEntry.UnlockedAt);

        // Verify FR-007a's attribution directly against the DB (not exposed on the response
        // contract, which callers don't need — only that it was recorded).
        var schemaName = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schemaName);
        var stored = await db.StaffTimeEntries.FirstAsync(e => e.Id == entryId);
        Assert.NotNull(stored.UnlockedBy);

        var correction = await client.SendAsync(AuthedRequest(
            HttpMethod.Patch, $"/api/staff-time-entries/{entryId}", org.AccessToken,
            new UpdateStaffTimeEntryRequest(null, null, null, "corrected")));
        Assert.Equal(HttpStatusCode.OK, correction.StatusCode);

        // No auto re-lock: still editable after the correction.
        var secondCorrection = await client.SendAsync(AuthedRequest(
            HttpMethod.Patch, $"/api/staff-time-entries/{entryId}", org.AccessToken,
            new UpdateStaffTimeEntryRequest(null, null, null, "corrected again")));
        Assert.Equal(HttpStatusCode.OK, secondCorrection.StatusCode);

        var relock = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/staff-time-entries/{entryId}/relock", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, relock.StatusCode);
        var relockedEntry = (await relock.Content.ReadFromJsonAsync<StaffTimeEntryResponse>())!;
        Assert.True(relockedEntry.IsLocked);

        var afterRelock = await client.SendAsync(AuthedRequest(
            HttpMethod.Patch, $"/api/staff-time-entries/{entryId}", org.AccessToken,
            new UpdateStaffTimeEntryRequest(null, null, null, "should fail")));
        Assert.Equal((HttpStatusCode)423, afterRelock.StatusCode);
    }

    [Fact]
    public async Task Update_CorrectedFunctionNotConfiguredForStaff_Rejected()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Lock Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var staff = await CreateStaffAsync(client, org.AccessToken);
        await AssignEligibilityAsync(client, org.AccessToken, staff.Id, location.Id);
        await SetFunctionsAsync(client, org.AccessToken, staff.Id, "kinderbegeleider");

        var entryId = await SeedEntryAsync(org.Organisation.Id, staff.Id, location.Id, DateTime.UtcNow.AddHours(-1));

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Patch, $"/api/staff-time-entries/{entryId}", org.AccessToken,
            new UpdateStaffTimeEntryRequest(null, "verantwoordelijke", null, null)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
