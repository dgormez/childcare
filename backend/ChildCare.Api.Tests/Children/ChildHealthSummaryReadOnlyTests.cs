using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.Children;

/// <summary>A device credential (caregiver-tablet kiosk mode) can never write a vaccine or
/// health record — unaffected by 031-photo-lifecycle-governance's staff-JWT widening, which only
/// applies to StaffOrDirector routes, not DeviceAuthenticated ones. (A staff-JWT account CAN now
/// create/edit/delete these records at its assigned location — see PhotoRbacParityTests.)</summary>
public class ChildHealthSummaryReadOnlyTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task DeviceToken_CannotCreateVaccineOrHealthRecord()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"ReadOnly Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Room 1", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var vaccineResponse = await client.SendAsync(DeviceRequest(HttpMethod.Post, $"/api/children/{child.Id}/vaccine-records", deviceToken,
            new CreateVaccineRecordRequest("DTP", null, DateOnly.FromDateTime(DateTime.UtcNow), null, null, null)));
        Assert.Equal(HttpStatusCode.Forbidden, vaccineResponse.StatusCode);

        var healthResponse = await client.SendAsync(DeviceRequest(HttpMethod.Post, $"/api/children/{child.Id}/health-records", deviceToken,
            new CreateHealthRecordRequest("allergy", "Title", "Description", null, null)));
        Assert.Equal(HttpStatusCode.Forbidden, healthResponse.StatusCode);
    }
}
