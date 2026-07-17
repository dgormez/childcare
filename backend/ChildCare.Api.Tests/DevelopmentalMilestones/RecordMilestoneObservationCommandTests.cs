using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.DevelopmentalMilestones;

/// <summary>User Story 1 (spec.md): caregiver records a milestone observation.</summary>
public class RecordMilestoneObservationCommandTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private async Task<(string AccessToken, string DeviceToken, Guid ChildId, Guid MilestoneId)> SetupAsync()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Milestones Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var caregiver = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        var checkIn = await CheckInAsync(client, deviceToken, caregiver.Id, "1234");
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);

        var domainsResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/developmental-domains", org.AccessToken));
        var domains = (await domainsResponse.Content.ReadFromJsonAsync<List<DevelopmentalDomainResponse>>())!;
        var milestoneId = domains.First().Milestones.First().Id;

        return (org.AccessToken, deviceToken, child.Id, milestoneId);
    }

    private static HttpRequestMessage RecordRequest(string deviceToken, Guid childId, Guid milestoneId, string status, DateOnly? observedAt = null, string? notes = null) =>
        DeviceRequest(HttpMethod.Post, $"/api/children/{childId}/milestone-observations", deviceToken, new
        {
            milestoneId,
            status,
            observedAt = observedAt ?? DateOnly.FromDateTime(DateTime.UtcNow),
            notes,
        });

    [Fact]
    public async Task RecordObservation_Persists_WithObservedByFromCheckedInShift()
    {
        var (_, deviceToken, childId, milestoneId) = await SetupAsync();
        var client = factory.CreateClient();

        var response = await client.SendAsync(RecordRequest(deviceToken, childId, milestoneId, "achieved"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var observation = (await response.Content.ReadFromJsonAsync<MilestoneObservationResponse>())!;
        Assert.Equal("achieved", observation.Status);
    }

    [Fact]
    public async Task RecordSecondObservation_ForSameMilestone_CreatesNewRow_LeavesFirstUnmodified()
    {
        var (accessToken, deviceToken, childId, milestoneId) = await SetupAsync();
        var client = factory.CreateClient();

        var first = await client.SendAsync(RecordRequest(deviceToken, childId, milestoneId, "achieved"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstObservation = (await first.Content.ReadFromJsonAsync<MilestoneObservationResponse>())!;

        var second = await client.SendAsync(RecordRequest(deviceToken, childId, milestoneId, "not_yet"));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var portfolioResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/children/{childId}/milestone-portfolio", accessToken));
        var portfolio = (await portfolioResponse.Content.ReadFromJsonAsync<MilestonePortfolioResponse>())!;
        var milestone = portfolio.Domains.SelectMany(d => d.Milestones).Single(m => m.Id == milestoneId);

        Assert.Equal("not_yet", milestone.CurrentStatus); // latest wins
        Assert.Equal(2, milestone.History!.Count); // both preserved
        Assert.Contains(milestone.History, h => h.Id == firstObservation.Id && h.Status == "achieved");
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("")]
    public async Task RecordObservation_WithInvalidStatus_IsRejectedByValidation(string status)
    {
        var (_, deviceToken, childId, milestoneId) = await SetupAsync();
        var client = factory.CreateClient();

        var response = await client.SendAsync(RecordRequest(deviceToken, childId, milestoneId, status));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode); // FluentValidation pipeline convention (ValidationBehavior)
    }

    [Fact]
    public async Task NoUpdateOrDeleteRouteExists_ForMilestoneObservations()
    {
        var (_, deviceToken, childId, milestoneId) = await SetupAsync();
        var client = factory.CreateClient();

        var created = await client.SendAsync(RecordRequest(deviceToken, childId, milestoneId, "achieved"));
        var observation = (await created.Content.ReadFromJsonAsync<MilestoneObservationResponse>())!;

        var patch = await client.SendAsync(DeviceRequest(HttpMethod.Patch, $"/api/children/{childId}/milestone-observations/{observation.Id}", deviceToken, new { status = "not_yet" }));
        var delete = await client.SendAsync(DeviceRequest(HttpMethod.Delete, $"/api/children/{childId}/milestone-observations/{observation.Id}", deviceToken));

        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode); // no such route mapped (research.md R3)
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }
}
