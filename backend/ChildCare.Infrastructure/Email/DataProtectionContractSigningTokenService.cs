using ChildCare.Application.Common;
using Microsoft.AspNetCore.DataProtection;

namespace ChildCare.Infrastructure.Email;

/// <summary>
/// Implements IContractSigningTokenService via ASP.NET Core's Data Protection API (feature
/// 024-esignature, research.md R2) — same idiom as DataProtectionTourInvitationTokenService
/// (feature 023)/DataProtectionUnsubscribeTokenService (feature 020). 72-hour lifetime per
/// FR-003. Single-use enforcement is a separate, application-layer concern (Contract.SigningToken
/// column) — this service only proves the token is well-formed, unexpired, and unmodified.
/// </summary>
public class DataProtectionContractSigningTokenService : IContractSigningTokenService
{
    private const string Purpose = "Contract.Signing";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(72);

    private readonly ITimeLimitedDataProtector _protector;

    public DataProtectionContractSigningTokenService(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose).ToTimeLimitedDataProtector();
    }

    public string CreateToken(Guid contractId) =>
        _protector.Protect(contractId.ToString(), TokenLifetime);

    public Guid? TryParseToken(string token)
    {
        try
        {
            var plaintext = _protector.Unprotect(token);
            return Guid.TryParse(plaintext, out var contractId) ? contractId : null;
        }
        catch
        {
            return null;
        }
    }
}
