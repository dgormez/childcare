using ChildCare.Application.Common;
using Microsoft.AspNetCore.DataProtection;

namespace ChildCare.Infrastructure.Payments;

/// <summary>
/// Encrypts/decrypts Mollie OAuth tokens at rest via ASP.NET Core's built-in Data Protection
/// API (research.md R3) — the first per-tenant third-party credential this codebase stores, and
/// Data Protection is already part of the framework, so no new dependency is introduced.
/// </summary>
public class PaymentTokenProtector : IPaymentTokenProtector
{
    private const string Purpose = "PaymentProviderConnection.MollieTokens";

    private readonly IDataProtector _protector;

    public PaymentTokenProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
