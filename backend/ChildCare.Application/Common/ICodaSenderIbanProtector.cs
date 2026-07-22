namespace ChildCare.Application.Common;

/// <summary>
/// Encrypts/decrypts a CODA transaction's sender IBAN at rest (feature 025, research.md R2). Its
/// own purpose string, distinct from IIbanProtector's (Contract.SepaIban) — a bank-statement
/// counterparty account and a signed SEPA mandate's account are different data, even though both
/// happen to be IBANs, so their ciphertexts are never interchangeable. Mirrors
/// IIbanProtector/INrnProtector/IPaymentTokenProtector's identical port/adapter shape.
/// </summary>
public interface ICodaSenderIbanProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
