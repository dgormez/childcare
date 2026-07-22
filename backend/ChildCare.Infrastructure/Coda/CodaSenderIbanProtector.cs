using ChildCare.Application.Common;
using Microsoft.AspNetCore.DataProtection;

namespace ChildCare.Infrastructure.Coda;

/// <summary>
/// Encrypts/decrypts a CODA transaction's sender IBAN at rest via ASP.NET Core's Data Protection
/// API (feature 025, research.md R2) — reuses the same mechanism as IbanProtector (024)/
/// NrnProtector (022)/PaymentTokenProtector (014a) under its own purpose string.
/// </summary>
public class CodaSenderIbanProtector : ICodaSenderIbanProtector
{
    private const string Purpose = "CodaTransaction.SenderIban";

    private readonly IDataProtector _protector;

    public CodaSenderIbanProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
