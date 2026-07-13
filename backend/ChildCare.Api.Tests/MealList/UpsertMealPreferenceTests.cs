using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.MealList.MealListTestSupport;

namespace ChildCare.Api.Tests.MealList;

/// <summary>User Story 3 (spec.md): director create/update of a child's meal preferences,
/// including partial-upsert semantics and validation.</summary>
public class UpsertMealPreferenceTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Upsert_NoExistingRow_CreatesWithSubmittedValues()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Meal Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null);

        var response = await UpsertMealPreferenceAsync(
            client, org.AccessToken, child.Id, texture: "mixed", dietaryType: ["halal"], portionSize: "small", additionalNotes: "No dairy at breakfast");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = (await response.Content.ReadFromJsonAsync<MealPreferenceResponse>())!;
        Assert.Equal("mixed", result.Texture);
        Assert.Equal(["halal"], result.DietaryType);
        Assert.Equal("small", result.PortionSize);
        Assert.Equal("No dairy at breakfast", result.AdditionalNotes);
    }

    [Fact]
    public async Task Upsert_SecondCallWithOnlyTexture_LeavesOtherFieldsUnchanged()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Meal Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null);

        await UpsertMealPreferenceAsync(
            client, org.AccessToken, child.Id, texture: "mixed", dietaryType: ["halal"], portionSize: "small", additionalNotes: "Note");

        var second = await UpsertMealPreferenceAsync(client, org.AccessToken, child.Id, texture: "pieces");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var result = (await second.Content.ReadFromJsonAsync<MealPreferenceResponse>())!;
        Assert.Equal("pieces", result.Texture);
        Assert.Equal(["halal"], result.DietaryType);
        Assert.Equal("small", result.PortionSize);
        Assert.Equal("Note", result.AdditionalNotes);
    }

    [Fact]
    public async Task Upsert_AdditionalNotesOverMaxLength_ReturnsUnprocessableEntityAndWritesNothing()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Meal Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null);

        var response = await UpsertMealPreferenceAsync(client, org.AccessToken, child.Id, additionalNotes: new string('a', 2001));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Get_NoExistingRow_ReturnsColumnDefaultsNot404()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Meal Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null);

        var response = await GetMealPreferenceAsync(client, org.AccessToken, child.Id);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = (await response.Content.ReadFromJsonAsync<MealPreferenceResponse>())!;
        Assert.Equal("normal", result.Texture);
        Assert.Empty(result.DietaryType);
        Assert.Equal("normal", result.PortionSize);
    }

    [Fact]
    public async Task Get_AfterUpsert_ReturnsCurrentValues()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Meal Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null);

        await UpsertMealPreferenceAsync(client, org.AccessToken, child.Id, texture: "pieces", portionSize: "large");

        var response = await GetMealPreferenceAsync(client, org.AccessToken, child.Id);
        var result = (await response.Content.ReadFromJsonAsync<MealPreferenceResponse>())!;
        Assert.Equal("pieces", result.Texture);
        Assert.Equal("large", result.PortionSize);
    }

    [Fact]
    public async Task Upsert_NonDirectorCaller_Returns403()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Meal Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var child = await CreateChildWithAllergySeverityAsync(client, org.AccessToken, null);
        var (_, _, parentToken) = await ParentTestSupport.InviteAndLoginParentAsync(
            client, factory, org.Organisation.Slug, org.AccessToken);

        var response = await UpsertMealPreferenceAsync(client, parentToken, child.Id, texture: "mixed");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
