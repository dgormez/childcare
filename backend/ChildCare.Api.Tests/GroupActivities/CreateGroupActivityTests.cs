using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.GroupActivities.GroupActivityTestSupport;

namespace ChildCare.Api.Tests.GroupActivities;

/// <summary>User Story 1 (spec.md FR-001/FR-002/FR-006) — create + recorded_by resolution + idempotency + validation.</summary>
public class CreateGroupActivityTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Create_TwoCaregiversCheckedIn_RecordedByHasBoth()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"GA Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var caregiver1 = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1111", "Marie");
        var caregiver2 = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "2222", "Thomas");
        Assert.Equal(HttpStatusCode.OK, (await CheckInAsync(client, deviceToken, caregiver1.Id, "1111")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CheckInAsync(client, deviceToken, caregiver2.Id, "2222")).StatusCode);

        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "outdoor", "In de tuin");

        Assert.Equal(2, activity.RecordedBy.Count);
        Assert.Contains(caregiver1.Id, activity.RecordedBy);
        Assert.Contains(caregiver2.Id, activity.RecordedBy);
    }

    [Fact]
    public async Task Create_NoCaregiverCheckedIn_RecordedByEmpty_NotBlocked()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"GA Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "music", "Muzikant");

        Assert.Empty(activity.RecordedBy);
    }

    [Fact]
    public async Task Create_IdempotentByClientId_RetryReturnsExistingRecord()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"GA Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var clientId = Guid.NewGuid();
        var first = await CreateGroupActivityOkAsync(client, deviceToken, "creative", "Tekenen", id: clientId);

        var retryResponse = await CreateGroupActivityAsync(client, deviceToken, "creative", "Tekenen (different title ignored)", id: clientId);
        Assert.True(retryResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created);
        var retry = (await retryResponse.Content.ReadFromJsonAsync<GroupActivityResponse>())!;

        Assert.Equal(first.Id, retry.Id);
        Assert.Equal("Tekenen", retry.Title);
    }

    [Fact]
    public async Task Create_TitleTooLong_ReturnsValidationError()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"GA Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await CreateGroupActivityAsync(client, deviceToken, "other", new string('x', 201));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Create_InvalidActivityType_ReturnsBadRequest()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"GA Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await CreateGroupActivityAsync(client, deviceToken, "not_a_real_type", "Title");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
