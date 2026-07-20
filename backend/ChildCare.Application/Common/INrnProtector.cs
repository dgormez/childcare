namespace ChildCare.Application.Common;

/// <summary>
/// Encrypts/decrypts a child's National Register Number at rest (research.md R3). Implemented by
/// ChildCare.Infrastructure's NrnProtector (ASP.NET Core Data Protection) — Application depends
/// on this abstraction, not the concrete Data-Protection-backed class, mirroring
/// IPaymentTokenProtector.
/// </summary>
public interface INrnProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
