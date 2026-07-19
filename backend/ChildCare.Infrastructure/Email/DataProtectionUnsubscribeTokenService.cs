using ChildCare.Application.Common;
using Microsoft.AspNetCore.DataProtection;

namespace ChildCare.Infrastructure.Email;

/// <summary>
/// Implements IUnsubscribeTokenService via ASP.NET Core's built-in Data Protection API
/// (feature 020, research.md R5) — same idiom as PaymentTokenProtector (feature 014a). The
/// `CreateProtector` purpose string is the token's "purpose scoping" (FR-007's "single-purpose"
/// requirement): a token protected under this purpose fails to unprotect under any other,
/// without needing a separate purpose field in the payload itself.
/// </summary>
public class DataProtectionUnsubscribeTokenService : IUnsubscribeTokenService
{
    private const string Purpose = "Email.DigestUnsubscribe";

    private readonly IDataProtector _protector;

    public DataProtectionUnsubscribeTokenService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string CreateToken(Guid contactId) => _protector.Protect(contactId.ToString());

    public Guid? TryParseToken(string token)
    {
        try
        {
            var plaintext = _protector.Unprotect(token);
            return Guid.TryParse(plaintext, out var contactId) ? contactId : null;
        }
        catch
        {
            return null;
        }
    }
}
