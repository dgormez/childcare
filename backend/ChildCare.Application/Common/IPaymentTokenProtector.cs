namespace ChildCare.Application.Common;

/// <summary>
/// Encrypts/decrypts Mollie OAuth tokens at rest (research.md R3). Implemented by
/// ChildCare.Infrastructure's PaymentTokenProtector (ASP.NET Core Data Protection) —
/// Application depends on this abstraction, not the concrete Data-Protection-backed class,
/// mirroring every other port/adapter split in this codebase (IPaymentProvider, IExpoPushSender).
/// </summary>
public interface IPaymentTokenProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
