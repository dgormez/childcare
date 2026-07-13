using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.Children;

/// <summary>User Story 4 (spec.md FR-013): expired health records and vaccines with no
/// upcoming/overdue due date are excluded from the caregiver summary.</summary>
public class ChildHealthSummaryFilteringTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task HealthSummary_ExcludesExpiredHealthRecordsAndNotDueVaccines()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Filtering Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room 1", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/groups", org.AccessToken,
            new AssignChildToGroupRequest(group.Id, new DateOnly(2026, 1, 1))));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Expired health record — excluded.
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("medication_standing", "Old prescription", "No longer active.",
                today.AddDays(-100), today.AddDays(-30))));
        // Current health record — included.
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", org.AccessToken,
            new CreateHealthRecordRequest("allergy", "Peanut allergy", "Confirmed.", null, null)));

        // Vaccine due far in the future (>30 days) — excluded from due-soon flags.
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", org.AccessToken,
            new CreateVaccineRecordRequest("Hep B", null, today.AddDays(-30), today.AddDays(90), null, null)));
        // Vaccine due soon — included.
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", org.AccessToken,
            new CreateVaccineRecordRequest("MMR", null, today.AddDays(-30), today.AddDays(10), null, null)));

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{child.Id}/health-summary", org.AccessToken));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = (await response.Content.ReadFromJsonAsync<ChildHealthSummaryResponse>())!;

        Assert.Single(summary.ActiveHealthRecords);
        Assert.Equal("Peanut allergy", summary.ActiveHealthRecords[0].Title);

        Assert.Single(summary.DueSoonVaccines);
        Assert.Equal("MMR", summary.DueSoonVaccines[0].VaccineName);
    }
}
