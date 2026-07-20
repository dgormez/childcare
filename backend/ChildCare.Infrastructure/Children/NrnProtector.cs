using ChildCare.Application.Common;
using Microsoft.AspNetCore.DataProtection;

namespace ChildCare.Infrastructure.Children;

/// <summary>
/// Encrypts/decrypts a child's National Register Number at rest via ASP.NET Core's built-in Data
/// Protection API (research.md R3) — reuses the same mechanism as PaymentTokenProtector (014a)
/// under its own purpose string, so the two ciphertexts are never interchangeable.
/// </summary>
public class NrnProtector : INrnProtector
{
    private const string Purpose = "Child.NationalRegisterNumber";

    private readonly IDataProtector _protector;

    public NrnProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string Protect(string plaintext) => _protector.Protect(plaintext);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}
