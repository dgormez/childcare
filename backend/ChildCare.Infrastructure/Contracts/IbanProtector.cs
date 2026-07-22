using ChildCare.Application.Common;
using Microsoft.AspNetCore.DataProtection;

namespace ChildCare.Infrastructure.Contracts;

/// <summary>
/// Encrypts/decrypts a SEPA mandate's IBAN at rest via ASP.NET Core's Data Protection API
/// (feature 024-esignature, research.md R3) — reuses the same mechanism as NrnProtector (022)/
/// PaymentTokenProtector (014a) under its own purpose string, so ciphertexts are never
/// interchangeable across features.
/// </summary>
public class IbanProtector : IIbanProtector
{
    private const string Purpose = "Contract.SepaIban";

    private readonly IDataProtector _protector;

    public IbanProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
