namespace ChildCare.Application.Common;

/// <summary>
/// Encrypts/decrypts a SEPA mandate's IBAN at rest (feature 024-esignature, research.md R3).
/// Implemented by ChildCare.Infrastructure's IbanProtector (ASP.NET Core Data Protection, its
/// own purpose string) — mirrors INrnProtector/IPaymentTokenProtector exactly. Application
/// depends on this abstraction, never the concrete Data-Protection-backed class.
/// </summary>
public interface IIbanProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
