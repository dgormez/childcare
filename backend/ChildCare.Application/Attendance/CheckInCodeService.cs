using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace ChildCare.Application.Attendance;

public record IssuedCheckInCode(string Code, long ExpiresAtUnix);

public enum CheckInCodeVerificationFailure
{
    InvalidSignature,
    Expired,
    AlreadyUsed,
}

public class CheckInCodeVerificationResult
{
    public Guid? ChildId { get; private init; }
    public string? Nonce { get; private init; }
    public CheckInCodeVerificationFailure? Failure { get; private init; }
    public bool Succeeded => Failure is null;

    public static CheckInCodeVerificationResult Success(Guid childId, string nonce) => new() { ChildId = childId, Nonce = nonce };
    public static CheckInCodeVerificationResult Fail(CheckInCodeVerificationFailure failure) => new() { Failure = failure };
}

public interface ICheckInCodeService
{
    IssuedCheckInCode Issue(Guid childId);
    CheckInCodeVerificationResult Verify(string code);

    // Marks a nonce as consumed so a second verify of the same code within the cooldown window
    // fails with AlreadyUsed (FR-019) — called only after a scan has been successfully dispatched
    // to CheckInCommand/CheckOutCommand, never on a rejected verification.
    void MarkConsumed(string nonce);
}

/// <summary>
/// Feature 021 — research.md R1. A signed, self-contained token (HMAC-SHA256) rather than a
/// persisted entity: verification is pure computation (decode, verify signature, check
/// timestamp), no database round-trip needed on the hot scan-to-confirmation path (SC-003). The
/// consumed-nonce cooldown set (FR-019) is the one piece of "have we seen this before" state
/// this feature actually needs, kept in an in-memory cache rather than a full table — see
/// research.md R1's "Alternatives considered".
/// </summary>
public class CheckInCodeService(IConfiguration configuration, IMemoryCache cache) : ICheckInCodeService
{
    private const int ValiditySeconds = 30;
    // Slightly longer than the code's own validity window so a code cannot be replayed the
    // instant it expires and cycles out of the cooldown set.
    private static readonly TimeSpan CooldownDuration = TimeSpan.FromSeconds(ValiditySeconds + 15);

    private readonly byte[] _signingKey = Encoding.UTF8.GetBytes(
        configuration["QrCheckIn:SigningKey"] ?? throw new InvalidOperationException("QrCheckIn:SigningKey is not configured."));

    public IssuedCheckInCode Issue(Guid childId)
    {
        var issuedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var nonce = Guid.NewGuid().ToString("N");
        var payload = $"{childId:N}|{issuedAtUnix}|{nonce}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = HMACSHA256.HashData(_signingKey, payloadBytes);

        var code = $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signature)}";
        return new IssuedCheckInCode(code, issuedAtUnix + ValiditySeconds);
    }

    public CheckInCodeVerificationResult Verify(string code)
    {
        var parts = code.Split('.', 2);
        if (parts.Length != 2)
            return CheckInCodeVerificationResult.Fail(CheckInCodeVerificationFailure.InvalidSignature);

        byte[] payloadBytes;
        byte[] signature;
        try
        {
            payloadBytes = Base64UrlDecode(parts[0]);
            signature = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            return CheckInCodeVerificationResult.Fail(CheckInCodeVerificationFailure.InvalidSignature);
        }

        var expectedSignature = HMACSHA256.HashData(_signingKey, payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(signature, expectedSignature))
            return CheckInCodeVerificationResult.Fail(CheckInCodeVerificationFailure.InvalidSignature);

        var payload = Encoding.UTF8.GetString(payloadBytes);
        var payloadParts = payload.Split('|');
        if (payloadParts.Length != 3
            || !Guid.TryParse(payloadParts[0], out var childId)
            || !long.TryParse(payloadParts[1], out var issuedAtUnix))
        {
            return CheckInCodeVerificationResult.Fail(CheckInCodeVerificationFailure.InvalidSignature);
        }

        var nonce = payloadParts[2];

        if (cache.TryGetValue(CacheKey(nonce), out _))
            return CheckInCodeVerificationResult.Fail(CheckInCodeVerificationFailure.AlreadyUsed);

        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - issuedAtUnix > ValiditySeconds)
            return CheckInCodeVerificationResult.Fail(CheckInCodeVerificationFailure.Expired);

        return CheckInCodeVerificationResult.Success(childId, nonce);
    }

    public void MarkConsumed(string nonce) =>
        cache.Set(CacheKey(nonce), true, CooldownDuration);

    private static string CacheKey(string nonce) => $"qr-checkin-code:{nonce}";

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}
