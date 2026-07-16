using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChildCare.Application.Common;
using Microsoft.Extensions.Configuration;

namespace ChildCare.Infrastructure.Payments;

/// <summary>
/// Thin HTTP adapter over Mollie's OAuth (Connect for Platforms) + Payments REST APIs
/// (research.md R1). No Mollie SDK dependency — a plain HttpClient-based port, matching this
/// codebase's existing IExpoPushSender/ExpoPushSender pattern. Client ID/secret come from
/// configuration (Secret Manager / Terraform-provisioned, constitution Principle VI), never
/// hardcoded.
/// </summary>
public class MolliePaymentProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IPaymentProvider
{
    private const string AuthorizeUrl = "https://www.mollie.com/oauth2/authorize";
    private const string TokenUrl = "https://api.mollie.com/oauth2/tokens";
    private const string PaymentsUrl = "https://api.mollie.com/v2/payments";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string GetOAuthAuthorizationUrl(string state)
    {
        var clientId = RequireConfig("Mollie:ClientId");
        var redirectUri = RequireConfig("Mollie:RedirectUri");
        var query = $"client_id={Uri.EscapeDataString(clientId)}" +
                    $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                    "&response_type=code" +
                    "&scope=payments.read+payments.write+organizations.read" +
                    $"&state={Uri.EscapeDataString(state)}";
        return $"{AuthorizeUrl}?{query}";
    }

    public async Task<PaymentProviderOAuthResult> CompleteOAuthConnectionAsync(
        string authorizationCode, CancellationToken cancellationToken = default)
    {
        var http = httpClientFactory.CreateClient();
        var clientId = RequireConfig("Mollie:ClientId");
        var clientSecret = RequireConfig("Mollie:ClientSecret");
        var redirectUri = RequireConfig("Mollie:RedirectUri");

        var tokenResponse = await http.PostAsync(TokenUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = authorizationCode,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        }), cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
            return new PaymentProviderOAuthResult(false, null, null, null, null, null);

        var token = await tokenResponse.Content.ReadFromJsonAsync<MollieTokenResponse>(JsonOptions, cancellationToken);
        if (token is null)
            return new PaymentProviderOAuthResult(false, null, null, null, null, null);

        var accountRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.mollie.com/v2/organizations/me");
        accountRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var accountResponse = await http.SendAsync(accountRequest, cancellationToken);
        if (!accountResponse.IsSuccessStatusCode)
            return new PaymentProviderOAuthResult(false, null, null, null, null, null);

        var account = await accountResponse.Content.ReadFromJsonAsync<MollieOrganizationResponse>(JsonOptions, cancellationToken);
        if (account is null)
            return new PaymentProviderOAuthResult(false, null, null, null, null, null);

        return new PaymentProviderOAuthResult(
            true,
            account.Id,
            account.Name,
            token.AccessToken,
            token.RefreshToken,
            DateTime.UtcNow.AddSeconds(token.ExpiresIn));
    }

    public async Task<PaymentProviderCreatePaymentResult> CreatePaymentAsync(
        PaymentProviderCredentials credentials, Guid paymentReference, int amountCents, string description,
        string webhookUrl, CancellationToken cancellationToken = default)
    {
        var http = httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, PaymentsUrl)
        {
            Content = JsonContent.Create(new
            {
                amount = new { currency = "EUR", value = (amountCents / 100.0m).ToString("0.00") },
                description,
                webhookUrl,
                redirectUrl = webhookUrl,
                metadata = new { paymentReference },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);

        var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payment = await response.Content.ReadFromJsonAsync<MolliePaymentResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Mollie create-payment response could not be parsed.");
        var checkoutUrl = payment.Links.Checkout?.Href
            ?? throw new InvalidOperationException("Mollie create-payment response had no checkout link.");

        return new PaymentProviderCreatePaymentResult(payment.Id, checkoutUrl);
    }

    public async Task<PaymentProviderStatusResult> GetPaymentStatusAsync(
        PaymentProviderCredentials credentials, string providerPaymentId, CancellationToken cancellationToken = default)
    {
        var http = httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"{PaymentsUrl}/{providerPaymentId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);

        var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var payment = await response.Content.ReadFromJsonAsync<MolliePaymentResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Mollie get-payment response could not be parsed.");

        // Mollie reports the actual PSP fee asynchronously via settlements, not on the payment
        // resource itself — out of scope for this feature's fee-recording granularity
        // (research.md R6); FeeCents stays null until a future settlement-reconciliation pass.
        var checkoutUrl = payment.Status == "open" ? payment.Links.Checkout?.Href : null;
        return new PaymentProviderStatusResult(payment.Status, checkoutUrl, null);
    }

    private string RequireConfig(string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key} is not configured.");

    private record MollieTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private record MollieOrganizationResponse(string Id, string Name);

    private record MolliePaymentResponse(
        string Id,
        string Status,
        [property: JsonPropertyName("_links")] MollieLinks Links);

    private record MollieLinks(MollieLink? Checkout);

    private record MollieLink(string Href);
}
