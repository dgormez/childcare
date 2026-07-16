using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.Payments;

/// <summary>
/// Feature 014a — spec.md User Story 2 (director connects the organisation's Mollie account).
/// FR-001 (OAuth connect), FR-002 (status never leaks tokens), FR-003 (disconnect), Edge Cases
/// (reconnect updates the same row).
/// </summary>
public class PaymentConnectionTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private record PaymentConnectionStatusResponse(string Status, string? ProviderAccountLabel, DateTime? ConnectedAt);
    private record AuthorizationUrlResponse(string AuthorizationUrl);

    [Fact]
    public async Task GetConnection_NoConnection_ReturnsDisconnected()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Payment Conn Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/organisations/me/payment-connection", org.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = (await response.Content.ReadFromJsonAsync<PaymentConnectionStatusResponse>())!;
        Assert.Equal("disconnected", status.Status);
    }

    [Fact]
    public async Task Authorize_ReturnsAuthorizationUrl()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Payment Conn Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        var response = await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/organisations/me/payment-connection/authorize", org.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<AuthorizationUrlResponse>())!;
        Assert.Contains("fake-mollie.test", body.AuthorizationUrl);
    }

    [Fact]
    public async Task Callback_ValidCode_ConnectsAndNeverLeaksTokens()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Payment Conn Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        factory.Services.GetRequiredService<FakePaymentProvider>().OAuthShouldSucceed = true;

        var callbackResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/organisations/me/payment-connection/callback", org.AccessToken,
            new CompletePaymentConnectionRequest("fake-code")));
        Assert.Equal(HttpStatusCode.OK, callbackResponse.StatusCode);
        var body = await callbackResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("fake-access-token", body);
        Assert.DoesNotContain("fake-refresh-token", body);

        var statusResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/organisations/me/payment-connection", org.AccessToken));
        var status = (await statusResponse.Content.ReadFromJsonAsync<PaymentConnectionStatusResponse>())!;
        Assert.Equal("connected", status.Status);
        Assert.Equal(factory.Services.GetRequiredService<FakePaymentProvider>().OAuthAccountLabel, status.ProviderAccountLabel);
    }

    [Fact]
    public async Task Callback_FailingCode_DoesNotConnect()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Payment Conn Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        factory.Services.GetRequiredService<FakePaymentProvider>().OAuthShouldSucceed = false;

        try
        {
            var callbackResponse = await client.SendAsync(AuthedRequest(
                HttpMethod.Post, "/api/organisations/me/payment-connection/callback", org.AccessToken,
                new CompletePaymentConnectionRequest("bad-code")));
            Assert.Equal(HttpStatusCode.UnprocessableEntity, callbackResponse.StatusCode);

            var statusResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/organisations/me/payment-connection", org.AccessToken));
            var status = (await statusResponse.Content.ReadFromJsonAsync<PaymentConnectionStatusResponse>())!;
            Assert.Equal("disconnected", status.Status);
        }
        finally
        {
            factory.Services.GetRequiredService<FakePaymentProvider>().OAuthShouldSucceed = true;
        }
    }

    [Fact]
    public async Task Disconnect_ThenReconnect_UpdatesSameConnection()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Payment Conn Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");

        await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/organisations/me/payment-connection/callback", org.AccessToken,
            new CompletePaymentConnectionRequest("fake-code")));

        var disconnectResponse = await client.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/organisations/me/payment-connection", org.AccessToken));
        Assert.Equal(HttpStatusCode.NoContent, disconnectResponse.StatusCode);

        var disconnectedStatusResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/organisations/me/payment-connection", org.AccessToken));
        var disconnectedStatus = (await disconnectedStatusResponse.Content.ReadFromJsonAsync<PaymentConnectionStatusResponse>())!;
        Assert.Equal("disconnected", disconnectedStatus.Status);

        var reconnectResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/organisations/me/payment-connection/callback", org.AccessToken,
            new CompletePaymentConnectionRequest("fake-code-2")));
        Assert.Equal(HttpStatusCode.OK, reconnectResponse.StatusCode);

        var reconnectedStatusResponse = await client.SendAsync(AuthedRequest(HttpMethod.Get, "/api/organisations/me/payment-connection", org.AccessToken));
        var reconnectedStatus = (await reconnectedStatusResponse.Content.ReadFromJsonAsync<PaymentConnectionStatusResponse>())!;
        Assert.Equal("connected", reconnectedStatus.Status);
    }
}
