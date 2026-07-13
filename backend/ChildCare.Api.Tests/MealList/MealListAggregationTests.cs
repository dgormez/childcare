using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.AttendanceTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.MealList.MealListTestSupport;

namespace ChildCare.Api.Tests.MealList;

/// <summary>User Story 1/2/4 (spec.md): the meal-list aggregation query's happy path and key
/// negative/safety flows.</summary>
public class MealListAggregationTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    [Fact]
    public async Task GetMealList_DeviceScopedToOneGroup_ReturnsOnlyThatGroupsPresentChildren()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Meal Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var groupA = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var groupB = await CreateGroupAsync(client, org.AccessToken, "Group B", location.Id);
        var (_, deviceA) = await PairDeviceAsync(client, org.AccessToken, location.Id, groupA.Id);

        var childInA = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null, "InGroupA");
        var childInB = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null, "InGroupB");
        await AssignChildToGroupAsync(client, org.AccessToken, childInA.Id, groupA.Id, Today);
        await AssignChildToGroupAsync(client, org.AccessToken, childInB.Id, groupB.Id, Today);

        Assert.Equal(HttpStatusCode.Created, (await CheckInChildAsync(client, deviceA, childInA.Id, Today)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await CheckInChildAsync(client, deviceA, childInB.Id, Today)).StatusCode);

        var response = await client.SendAsync(MealListRequest(deviceA, isDevice: true, location.Id, Today));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mealList = (await response.Content.ReadFromJsonAsync<MealListResponse>())!;

        var allChildIds = mealList.Groups.SelectMany(g => g.Children).Select(c => c.ChildId).ToList();
        Assert.Contains(childInA.Id, allChildIds);
        Assert.DoesNotContain(childInB.Id, allChildIds);
    }

    [Fact]
    public async Task GetMealList_ChildWithNoPreference_ShowsAsNoPreferenceRatherThanHidden()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Meal Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group", location.Id);
        var (_, device) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var child = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null);
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id, Today);
        await CheckInChildAsync(client, device, child.Id, Today);

        var response = await client.SendAsync(MealListRequest(org.AccessToken, isDevice: false, location.Id, Today));
        var mealList = (await response.Content.ReadFromJsonAsync<MealListResponse>())!;

        var entry = mealList.Groups.SelectMany(g => g.Children).Single(c => c.ChildId == child.Id);
        Assert.False(entry.HasPreference);
        Assert.Equal("normal", entry.Texture);
        Assert.Empty(entry.DietaryType);
        Assert.Equal("normal", entry.PortionSize);
    }

    [Fact]
    public async Task GetMealList_AllergySeverityMapping_MatchesChildAllergySeverity()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Meal Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group", location.Id);
        var (_, device) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var severeChild = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, "Severe", "Severe");
        var mildChild = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, "Mild", "Mild");
        var noneChild = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null, "None");

        foreach (var c in new[] { severeChild, mildChild, noneChild })
        {
            await AssignChildToGroupAsync(client, org.AccessToken, c.Id, group.Id, Today);
            await CheckInChildAsync(client, device, c.Id, Today);
        }

        var response = await client.SendAsync(MealListRequest(org.AccessToken, isDevice: false, location.Id, Today));
        var mealList = (await response.Content.ReadFromJsonAsync<MealListResponse>())!;
        var entries = mealList.Groups.SelectMany(g => g.Children).ToDictionary(c => c.ChildId);

        Assert.Equal("severe", entries[severeChild.Id].AllergySeverity);
        Assert.Equal("mild_moderate", entries[mildChild.Id].AllergySeverity);
        Assert.Equal("none", entries[noneChild.Id].AllergySeverity);
    }

    [Fact]
    public async Task GetMealList_StandingMedication_InclusiveBoundaryAndSingleIconNotCount()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Meal Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group", location.Id);
        var (_, device) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var child = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null, "Medicated");
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id, Today);
        await CheckInChildAsync(client, device, child.Id, Today);

        // Two simultaneously-valid records — must still surface as a single boolean, not a count.
        // ValidFrom/ValidUntil both equal to today — inclusive boundary must count as valid.
        Assert.Equal(HttpStatusCode.Created, (await CreateStandingMedicationAsync(client, org.AccessToken, child.Id, Today, Today)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await CreateStandingMedicationAsync(client, org.AccessToken, child.Id, Today.AddDays(-5), Today.AddDays(5))).StatusCode);

        var response = await client.SendAsync(MealListRequest(org.AccessToken, isDevice: false, location.Id, Today));
        var mealList = (await response.Content.ReadFromJsonAsync<MealListResponse>())!;
        var entry = mealList.Groups.SelectMany(g => g.Children).Single(c => c.ChildId == child.Id);
        Assert.True(entry.HasStandingMedication);

        var expiredChild = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null, "Expired");
        await AssignChildToGroupAsync(client, org.AccessToken, expiredChild.Id, group.Id, Today);
        await CheckInChildAsync(client, device, expiredChild.Id, Today);
        await CreateStandingMedicationAsync(client, org.AccessToken, expiredChild.Id, Today.AddDays(-10), Today.AddDays(-1));

        var response2 = await client.SendAsync(MealListRequest(org.AccessToken, isDevice: false, location.Id, Today));
        var mealList2 = (await response2.Content.ReadFromJsonAsync<MealListResponse>())!;
        var expiredEntry = mealList2.Groups.SelectMany(g => g.Children).Single(c => c.ChildId == expiredChild.Id);
        Assert.False(expiredEntry.HasStandingMedication);
    }

    [Fact]
    public async Task GetMealList_ParentRoleCaller_Returns403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Meal Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");

        var (_, _, parentToken) = await ParentTestSupport.InviteAndLoginParentAsync(
            client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await client.SendAsync(MealListRequest(parentToken, isDevice: false, location.Id, Today));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMealList_AbsentOrCheckedOutChild_NeverIncluded()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Meal Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group", location.Id);
        var (_, device) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var absentChild = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null, "Absent");
        await AssignChildToGroupAsync(client, org.AccessToken, absentChild.Id, group.Id, Today);
        await MarkAbsentAsDeviceAsync(client, device, absentChild.Id, location.Id, Today, justified: true);

        var checkedOutChild = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null, "CheckedOut");
        await AssignChildToGroupAsync(client, org.AccessToken, checkedOutChild.Id, group.Id, Today);
        await CheckInChildAsync(client, device, checkedOutChild.Id, Today);
        Assert.Equal(HttpStatusCode.OK, (await CheckOutChildAsync(client, device, checkedOutChild.Id, Today)).StatusCode);

        var response = await client.SendAsync(MealListRequest(org.AccessToken, isDevice: false, location.Id, Today));
        var mealList = (await response.Content.ReadFromJsonAsync<MealListResponse>())!;
        var allChildIds = mealList.Groups.SelectMany(g => g.Children).Select(c => c.ChildId).ToList();

        Assert.DoesNotContain(absentChild.Id, allChildIds);
        Assert.DoesNotContain(checkedOutChild.Id, allChildIds);
    }

    [Fact]
    public async Task GetMealList_DirectorCaller_ReturnsChildrenFromEveryGroup()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Meal Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var groupA = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var groupB = await CreateGroupAsync(client, org.AccessToken, "Group B", location.Id);
        var (_, device) = await PairDeviceAsync(client, org.AccessToken, location.Id, groupA.Id);

        var childInA = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null, "InGroupA");
        var childInB = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null, "InGroupB");
        await AssignChildToGroupAsync(client, org.AccessToken, childInA.Id, groupA.Id, Today);
        await AssignChildToGroupAsync(client, org.AccessToken, childInB.Id, groupB.Id, Today);
        await CheckInChildAsync(client, device, childInA.Id, Today);
        await CheckInChildAsync(client, device, childInB.Id, Today);

        var response = await client.SendAsync(MealListRequest(org.AccessToken, isDevice: false, location.Id, Today));
        var mealList = (await response.Content.ReadFromJsonAsync<MealListResponse>())!;

        Assert.Equal(2, mealList.Groups.Count);
        var allChildIds = mealList.Groups.SelectMany(g => g.Children).Select(c => c.ChildId).ToList();
        Assert.Contains(childInA.Id, allChildIds);
        Assert.Contains(childInB.Id, allChildIds);
    }

    [Fact]
    public async Task GetMealList_IncludeExpected_ShowsUncheckedInContractedChild_OnlyWhenToggled()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Meal Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");

        var expectedChild = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null, "Expected");
        await CreateAndActivateContractAsync(client, org.AccessToken, expectedChild.Id, location.Id, Today.DayOfWeek);

        var withoutToggle = await client.SendAsync(MealListRequest(org.AccessToken, isDevice: false, location.Id, Today));
        var mealListWithout = (await withoutToggle.Content.ReadFromJsonAsync<MealListResponse>())!;
        Assert.Null(mealListWithout.Expected);

        var withToggle = await client.SendAsync(MealListRequest(org.AccessToken, isDevice: false, location.Id, Today, includeExpected: true));
        var mealListWith = (await withToggle.Content.ReadFromJsonAsync<MealListResponse>())!;
        Assert.NotNull(mealListWith.Expected);
        Assert.Contains(mealListWith.Expected!.Children, c => c.ChildId == expectedChild.Id);
    }

    [Fact]
    public async Task GetMealList_IncludeExpected_NeverIncludesAbsentOrClosureChild()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Meal Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group", location.Id);
        var (_, device) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var absentChild = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null, "AbsentContracted");
        await CreateAndActivateContractAsync(client, org.AccessToken, absentChild.Id, location.Id, Today.DayOfWeek);
        await MarkAbsentAsDeviceAsync(client, device, absentChild.Id, location.Id, Today, justified: true);

        var response = await client.SendAsync(MealListRequest(org.AccessToken, isDevice: false, location.Id, Today, includeExpected: true));
        var mealList = (await response.Content.ReadFromJsonAsync<MealListResponse>())!;

        Assert.DoesNotContain(mealList.Expected?.Children ?? [], c => c.ChildId == absentChild.Id);
    }
}
