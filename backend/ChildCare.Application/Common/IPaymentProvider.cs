namespace ChildCare.Application.Common;

/// <summary>
/// Thin port over a payment service provider's Connect + Payments APIs (research.md R1),
/// mirroring IExpoPushSender's existing shape. No provider-specific type (Mollie's SDK/wire
/// shapes) crosses this boundary — a second provider (Stripe, POM) implements this same
/// interface without touching any caller (spec.md FR-018).
/// </summary>
public interface IPaymentProvider
{
    /// <summary>Builds the hosted OAuth consent URL a director is redirected to.</summary>
    string GetOAuthAuthorizationUrl(string state);

    /// <summary>Exchanges an OAuth authorization code for a connected account + tokens.</summary>
    Task<PaymentProviderOAuthResult> CompleteOAuthConnectionAsync(
        string authorizationCode, CancellationToken cancellationToken = default);

    /// <summary>Creates a hosted payment against the connected account's own funds.</summary>
    Task<PaymentProviderCreatePaymentResult> CreatePaymentAsync(
        PaymentProviderCredentials credentials,
        Guid paymentReference,
        int amountCents,
        string description,
        string webhookUrl,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches a payment's authoritative status directly from the provider — never
    /// trust a webhook payload's claimed status (spec.md FR-007).</summary>
    Task<PaymentProviderStatusResult> GetPaymentStatusAsync(
        PaymentProviderCredentials credentials,
        string providerPaymentId,
        CancellationToken cancellationToken = default);
}

/// <summary>Decrypted, request-scoped credentials for one connected account. Decryption
/// (research.md R3) happens at the command layer, not inside the provider adapter.</summary>
public record PaymentProviderCredentials(string AccessToken);

public record PaymentProviderOAuthResult(
    bool Succeeded,
    string? ProviderAccountId,
    string? ProviderAccountLabel,
    string? AccessToken,
    string? RefreshToken,
    DateTime? ExpiresAt);

public record PaymentProviderCreatePaymentResult(string ProviderPaymentId, string CheckoutUrl);

/// <summary>Status is one of "open"/"paid"/"failed"/"cancelled"/"expired" — the provider
/// adapter normalizes its own vocabulary to this shared one. CheckoutUrl is populated whenever
/// the provider still has one for this payment (i.e. Status == "open") — reusing it is how an
/// already-open payment is resurfaced without creating a second one (research.md R6).</summary>
public record PaymentProviderStatusResult(string Status, string? CheckoutUrl, int? FeeCents);
