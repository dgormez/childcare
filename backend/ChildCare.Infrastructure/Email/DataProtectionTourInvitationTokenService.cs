using ChildCare.Application.Common;
using Microsoft.AspNetCore.DataProtection;

namespace ChildCare.Infrastructure.Email;

/// <summary>
/// Implements ITourInvitationTokenService via ASP.NET Core's built-in Data Protection API
/// (feature 023, research.md R5) — same idiom as DataProtectionUnsubscribeTokenService (feature
/// 020) and PaymentTokenProtector (feature 014a). The `CreateProtector` purpose string scopes
/// the token to this single purpose: a token protected here fails to unprotect under any other
/// purpose, without needing a separate purpose field in the payload itself.
///
/// Unlike the unsubscribe token (deliberately permanent — a recipient should always be able to
/// unsubscribe), a tour invitation is tied to a specific proposed date and should not remain
/// actionable indefinitely (spec.md's Testing Requirements explicitly lists an "expired" case).
/// Uses `ToTimeLimitedDataProtector` with a generous 30-day lifetime — comfortably past any
/// realistic proposed-tour lead time, short enough that a stale link from months ago correctly
/// stops working. Expiry surfaces as a normal `CryptographicException` from `Unprotect`, caught
/// by the same fail-closed catch below that already handles tampering — the response page
/// already renders one generic "invalid or expired link" message for both (tasks.md T043).
/// </summary>
public class DataProtectionTourInvitationTokenService : ITourInvitationTokenService
{
    private const string Purpose = "WaitingList.TourInvitation";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(30);

    private readonly ITimeLimitedDataProtector _protector;

    public DataProtectionTourInvitationTokenService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose).ToTimeLimitedDataProtector();
    }

    public string CreateToken(Guid waitingListEntryId) =>
        _protector.Protect(waitingListEntryId.ToString(), TokenLifetime);

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
