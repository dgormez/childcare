using System.Collections.Concurrent;
using ChildCare.Application.Common;

namespace ChildCare.Api.Tests;

/// <summary>
/// Test double for IPaymentProvider — registered Singleton in OrganisationOnboardingWebAppFactory,
/// overriding Program.cs's real MolliePaymentProvider so tests never make a real outbound HTTP
/// call to Mollie (mirrors FakeExpoPushSender's pattern). Lets a test simulate Mollie confirming
/// a payment (or failing/cancelling it) by mutating the recorded FakePayment's Status directly,
/// then exercising ProcessPaymentWebhookCommand exactly as the real webhook flow would.
/// </summary>
public class FakePaymentProvider : IPaymentProvider
{
    public record FakePayment(string ProviderPaymentId, string CheckoutUrl, int AmountCents)
    {
        public string Status { get; set; } = "open";
    }

    public ConcurrentDictionary<string, FakePayment> Payments { get; } = new();

    public bool OAuthShouldSucceed { get; set; } = true;
    public string OAuthAccountId { get; set; } = "org_fake_1";
    public string OAuthAccountLabel { get; set; } = "Fake Test Organisation";

    public string GetOAuthAuthorizationUrl(string state) => $"https://fake-mollie.test/oauth2/authorize?state={state}";

    public Task<PaymentProviderOAuthResult> CompleteOAuthConnectionAsync(string authorizationCode, CancellationToken cancellationToken = default)
    {
        if (!OAuthShouldSucceed)
            return Task.FromResult(new PaymentProviderOAuthResult(false, null, null, null, null, null));

        return Task.FromResult(new PaymentProviderOAuthResult(
            true, OAuthAccountId, OAuthAccountLabel, "fake-access-token", "fake-refresh-token", DateTime.UtcNow.AddHours(1)));
    }

    public Task<PaymentProviderCreatePaymentResult> CreatePaymentAsync(
        PaymentProviderCredentials credentials, Guid paymentReference, int amountCents, string description,
        string webhookUrl, CancellationToken cancellationToken = default)
    {
        var providerPaymentId = $"tr_fake_{Guid.NewGuid():N}";
        var checkoutUrl = $"https://fake-mollie.test/checkout/{providerPaymentId}";
        Payments[providerPaymentId] = new FakePayment(providerPaymentId, checkoutUrl, amountCents);
        return Task.FromResult(new PaymentProviderCreatePaymentResult(providerPaymentId, checkoutUrl));
    }

    public Task<PaymentProviderStatusResult> GetPaymentStatusAsync(
        PaymentProviderCredentials credentials, string providerPaymentId, CancellationToken cancellationToken = default)
    {
        if (!Payments.TryGetValue(providerPaymentId, out var payment))
            return Task.FromResult(new PaymentProviderStatusResult("failed", null, null));

        var checkoutUrl = payment.Status == "open" ? payment.CheckoutUrl : null;
        return Task.FromResult(new PaymentProviderStatusResult(payment.Status, checkoutUrl, null));
    }
}
