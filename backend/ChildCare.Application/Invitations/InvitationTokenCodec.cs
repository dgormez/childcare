using System.Security.Cryptography;

namespace ChildCare.Application.Invitations;

/// <summary>
/// Symmetric encode (invitation creation) / decode+hash (registration lookup) for the opaque
/// invitation token (research.md R4) — kept in one place so the two sides can't drift apart.
/// </summary>
public static class InvitationTokenCodec
{
    public static (string PlaintextToken, byte[] Hash) Generate()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(64);
        return (Encode(tokenBytes), SHA256.HashData(tokenBytes));
    }

    /// <summary>Returns null if the token isn't validly-formed base64url — treat as "not found".</summary>
    public static byte[]? HashFromPlaintext(string plaintextToken)
    {
        try
        {
            var padded = plaintextToken.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            var tokenBytes = Convert.FromBase64String(padded);
            return SHA256.HashData(tokenBytes);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string Encode(byte[] tokenBytes)
        => Convert.ToBase64String(tokenBytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
