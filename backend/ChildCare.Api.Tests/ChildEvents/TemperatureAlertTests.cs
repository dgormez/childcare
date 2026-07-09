using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;

namespace ChildCare.Api.Tests.ChildEvents;

/// <summary>User Story 2 (T024-T026a): temperature threshold triggers a push-alert attempt (FR-010/FR-011/FR-011a/FR-011b).</summary>
public class TemperatureAlertTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private FakeExpoPushSender PushSender => factory.Services.GetRequiredService<FakeExpoPushSender>();

    [Fact]
    public async Task TemperatureAbove38_TriggersPushToEveryPickupEligibleContactWithToken()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"FeverOrg {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        var contact = await CreatePickupEligibleContactWithPushTokenAsync(client, factory.Services, org.AccessToken, child.Id, schema);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var before = PushSender.Sent.Count;
        var response = await PostChildEventAsync(client, deviceToken, child.Id, "temperature", DateTime.UtcNow, new { celsius = 38.5 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        Assert.Equal(before + 1, PushSender.Sent.Count);
        Assert.Contains(PushSender.Sent, s => s.PushToken == "ExponentPushToken[test]");
    }

    [Fact]
    public async Task TemperatureAtOrBelow38_DoesNotTriggerPush()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"NoFeverOrg {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        await CreatePickupEligibleContactWithPushTokenAsync(client, factory.Services, org.AccessToken, child.Id, schema);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var before = PushSender.Sent.Count;
        var response = await PostChildEventAsync(client, deviceToken, child.Id, "temperature", DateTime.UtcNow, new { celsius = 37.0 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(before, PushSender.Sent.Count);
    }

    [Fact]
    public async Task TemperatureAbove38_NoEligibleContacts_StillSavesSuccessfully()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"NoRecipientOrg {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken); // no contacts at all
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var response = await PostChildEventAsync(client, deviceToken, child.Id, "temperature", DateTime.UtcNow, new { celsius = 39.2 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task TemperatureAbove38_DispatchTransportFailure_StillSavesSuccessfully_AndTwoQualifyingEventsEachAttemptIndependently()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"DispatchFailOrg {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken);
        var schema = await GetSchemaNameAsync(factory.Services, org.Organisation.Id);
        await CreatePickupEligibleContactWithPushTokenAsync(client, factory.Services, org.AccessToken, child.Id, schema);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        PushSender.ThrowOnSend = true;
        try
        {
            var response1 = await PostChildEventAsync(client, deviceToken, child.Id, "temperature", DateTime.UtcNow, new { celsius = 38.6 });
            Assert.Equal(HttpStatusCode.Created, response1.StatusCode); // FR-011a: dispatch failure never fails the save

            var response2 = await PostChildEventAsync(client, deviceToken, child.Id, "temperature", DateTime.UtcNow.AddMinutes(5), new { celsius = 39.0 });
            Assert.Equal(HttpStatusCode.Created, response2.StatusCode); // FR-011b: no de-duplication, both events save
        }
        finally
        {
            PushSender.ThrowOnSend = false;
        }
    }
}
