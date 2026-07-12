using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.IncidentReports.IncidentReportTestSupport;

namespace ChildCare.Api.Tests.IncidentReports;

/// <summary>User Story 1 (T012-T017): caregiver files an incident report on the spot.</summary>
public class FileIncidentReportTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task File_HappyPath_ResolvesReportedByFromCheckedInCaregiver()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Incident Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        var caregiver = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "2468");
        Assert.Equal(HttpStatusCode.OK, (await CheckInAsync(client, deviceToken, caregiver.Id, "2468")).StatusCode);

        var response = await FileIncidentReportAsync(client, deviceToken, child.Id);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = (await response.Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        Assert.Equal(child.Id, body.ChildId);
        Assert.Equal(location.Id, body.LocationId);
        Assert.Contains(caregiver.Id, body.ReportedBy);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task File_MissingRequiredField_Returns422WithFieldSpecificError(bool omitDescription, bool omitInjuryType)
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Incident Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await client.SendAsync(DeviceRequest(HttpMethod.Post, "/api/incident-reports/", deviceToken, new
        {
            childId = child.Id,
            description = omitDescription ? "" : "Fell off the swing",
            injuryType = omitInjuryType ? "" : "fall",
        }));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task File_UnknownChildId_Returns404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Incident Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await FileIncidentReportAsync(client, deviceToken, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task File_NoCaregiverCheckedIn_ReportedByIsEmpty_NeverBlocked()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Incident Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await FileIncidentReportAsync(client, deviceToken, child.Id);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = (await response.Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        Assert.Empty(body.ReportedBy);
    }

    [Fact]
    public async Task File_ClientSuppliedReportedBy_IsIgnoredAndOverwrittenServerSide()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Incident Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        var spoofedId = Guid.NewGuid();

        var response = await client.SendAsync(DeviceRequest(HttpMethod.Post, "/api/incident-reports/", deviceToken, new
        {
            childId = child.Id,
            description = "Bumped head on the table.",
            injuryType = "bump",
            reportedBy = new[] { spoofedId },
        }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        Assert.DoesNotContain(spoofedId, body.ReportedBy);
    }

    [Fact]
    public async Task File_BackdatedOccurredAt_IsAcceptedAndTimestampsRemainIndependent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Incident Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var backdated = DateTime.UtcNow.AddHours(-6);
        var response = await FileIncidentReportAsync(client, deviceToken, child.Id, occurredAt: backdated);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = (await response.Content.ReadFromJsonAsync<IncidentReportResponse>())!;
        Assert.True(body.OccurredAt < body.CreatedAt);
        Assert.True((body.CreatedAt - body.OccurredAt) > TimeSpan.FromHours(5));
    }
}
