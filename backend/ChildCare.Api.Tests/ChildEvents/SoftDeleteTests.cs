using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.ChildEvents;

/// <summary>User Story 3 (T036): soft-delete — retained in the DB, excluded from all reads (FR-008).</summary>
public class SoftDeleteTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task DeletedEvent_ExcludedFromListAndDailySummary_ButRowRetainedWithDeletedAtSet()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"SoftDelete Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var occurredAt = DateTime.UtcNow;
        var createResponse = await PostChildEventAsync(client, deviceToken, child.Id, "diaper", occurredAt, new { type = "wet" });
        var created = (await createResponse.Content.ReadFromJsonAsync<ChildEventResponse>())!;

        var deleteResponse = await DeleteChildEventAsDeviceAsync(client, deviceToken, created.Id);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await GetChildEventsAsync(client, deviceToken, child.Id);
        var list = (await listResponse.Content.ReadFromJsonAsync<PagedChildEventsResponse>())!;
        Assert.DoesNotContain(list.Items, e => e.Id == created.Id);

        var summaryResponse = await GetDailySummaryAsync(client, deviceToken, child.Id, DateOnly.FromDateTime(occurredAt));
        var summary = (await summaryResponse.Content.ReadFromJsonAsync<DailySummaryResponse>())!;
        Assert.Equal(0, summary.DiaperChangesCount);

        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var db = ResolveTenantDb(factory.Services, schema);
        var row = await db.ChildEvents.SingleAsync(e => e.Id == created.Id);
        Assert.NotNull(row.DeletedAt);
    }
}
