using ChildCare.Application.Common;
using Microsoft.AspNetCore.DataProtection;

namespace ChildCare.Infrastructure.Email;

/// <summary>
/// Implements ITourInvitationTokenService via ASP.NET Core's built-in Data Protection API
/// (feature 023, research.md R5) — same idiom as DataProtectionUnsubscribeTokenService (feature
/// 020) and PaymentTokenProtector (feature 014a). The `CreateProtector` purpose string scopes
/// the token to this single purpose: a token protected here fails to unprotect under any other
/// purpose, without needing a separate purpose field in the payload itself.
/// </summary>
public class DataProtectionTourInvitationTokenService : ITourInvitationTokenService
{
    private const string Purpose = "WaitingList.TourInvitation";

    private readonly IDataProtector _protector;

    public DataProtectionTourInvitationTokenService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string CreateToken(Guid waitingListEntryId) => _protector.Protect(waitingListEntryId.ToString());

    public Guid? TryParseToken(string token)
    {
        try
        {
            var plaintext = _protector.Unprotect(token);
            return Guid.TryParse(plaintext, out var entryId) ? entryId : null;
        }
        catch
        {
            return null;
        }
    }
}
