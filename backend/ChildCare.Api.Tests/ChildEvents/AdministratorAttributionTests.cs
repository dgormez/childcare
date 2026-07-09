using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Responses;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.ChildEvents;

/// <summary>User Story 2 (T027): administratorByStaffId (from the reused confirm-administrator flow) persists as AdministeredBy.</summary>
public class AdministratorAttributionTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task MedicationEvent_WithConfirmedAdministrator_PersistsAdministeredBy()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"AdminAttrib Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var staff = await CreateEligibleCaregiverWithPinAsync(client, org.AccessToken, location.Id, "1234");
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);
        await CheckInAsync(client, deviceToken, staff.Id, "1234");

        var confirm = await ConfirmAdministratorAsync(client, deviceToken, staff.Id, "1234", false);
        var confirmBody = (await confirm.Content.ReadFromJsonAsync<ConfirmAdministratorResponse>())!;
        Assert.Equal(staff.Id, confirmBody.AdministeredByStaffProfileId);

        var response = await PostChildEventAsync(
            client, deviceToken, child.Id, "medication", DateTime.UtcNow,
            new { name = "perdolan", doseDescription = "5ml", reason = "fever" },
            administeredByStaffId: confirmBody.AdministeredByStaffProfileId);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = (await response.Content.ReadFromJsonAsync<ChildEventResponse>())!;
        Assert.Equal(staff.Id, body.AdministeredBy);
    }

    [Fact]
    public async Task MedicationEvent_Skipped_LeavesAdministeredByNull()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"AdminSkip Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await PostChildEventAsync(
            client, deviceToken, child.Id, "medication", DateTime.UtcNow,
            new { name = "perdolan", doseDescription = "5ml", reason = "fever" });
        var body = (await response.Content.ReadFromJsonAsync<ChildEventResponse>())!;
        Assert.Null(body.AdministeredBy);
    }
}
