using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ChildCare.Api.Data;
using ChildCare.Api.Models;

namespace ChildCare.Api.Services;

public class AuthService(
    AppDbContext db,
    JwtService jwt,
    EmailService emailService,
    IHttpClientFactory httpClientFactory,
    IConfiguration config)
{
    // ── Register ──────────────────────────────────────────────────────────────

    /// <summary>Returns null Response when the email is already taken.</summary>
    public async Task<(AuthResponse? Response, bool EmailExists)> RegisterAsync(string email, string password)
    {
        var normalized = email.ToLower().Trim();
        if (await db.Users.AnyAsync(u => u.Email == normalized))
            return (null, true);

        var user = new User
        {
            Email        = normalized,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        };

        db.Users.Add(user);

        // Send verification email before saving so the token is persisted in one transaction
        await SetVerificationTokenAsync(user);

        var refreshToken = AddRefreshToken(user);
        await db.SaveChangesAsync();

        await emailService.SendEmailVerificationAsync(user.Email, BuildVerifyUrl(user.EmailVerificationToken!));

        return (BuildAuthResponse(user, refreshToken), false);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    /// <summary>Returns null when credentials are invalid.</summary>
    public async Task<AuthResponse?> LoginAsync(string email, string password)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLower().Trim());
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return null;

        var refreshToken = AddRefreshToken(user);
        await db.SaveChangesAsync();
        return BuildAuthResponse(user, refreshToken);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    /// <summary>Returns null when the refresh token is invalid or expired.</summary>
    public async Task<AuthResponse?> RefreshAsync(string refreshToken)
    {
        var tokenEntity = await db.UserRefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == refreshToken);

        if (tokenEntity is null || tokenEntity.ExpiresAt < DateTime.UtcNow)
            return null;

        // Rotate: remove the used token and issue a fresh one
        db.UserRefreshTokens.Remove(tokenEntity);
        var newToken = AddRefreshToken(tokenEntity.User);
        await db.SaveChangesAsync();
        return BuildAuthResponse(tokenEntity.User, newToken);
    }

    // ── Google Sign In ────────────────────────────────────────────────────────

    /// <summary>Returns null when the Google ID token is invalid or the audience is not allowed.</summary>
    public async Task<AuthResponse?> GoogleSignInAsync(string idToken)
    {
        GoogleTokenInfo? payload;
        try
        {
            var http = httpClientFactory.CreateClient();
            var res  = await http.GetAsync($"https://oauth2.googleapis.com/tokeninfo?id_token={idToken}");
            if (!res.IsSuccessStatusCode) return null;
            payload = await res.Content.ReadFromJsonAsync<GoogleTokenInfo>();
        }
        catch { return null; }

        if (payload?.Email is null || payload.EmailVerified != "true") return null;

        var allowedIds = config.GetSection("Google:AllowedClientIds").Get<string[]>() ?? [];
        if (payload.Aud is null || !allowedIds.Contains(payload.Aud)) return null;

        var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleId == payload.Sub)
                ?? await db.Users.FirstOrDefaultAsync(u => u.Email    == payload.Email.ToLower());

        if (user is null)
        {
            // Google has already verified the email
            user = new User { Email = payload.Email.ToLower(), GoogleId = payload.Sub, PasswordHash = string.Empty, EmailVerified = true };
            db.Users.Add(user);
        }
        else
        {
            if (user.GoogleId is null) user.GoogleId = payload.Sub;
            // Mark verified if the account was created via email/password and not yet verified
            if (!user.EmailVerified) user.EmailVerified = true;
        }

        var refreshToken = AddRefreshToken(user);
        await db.SaveChangesAsync();
        return BuildAuthResponse(user, refreshToken);
    }

    // ── Apple Sign In ─────────────────────────────────────────────────────────

    /// <summary>Returns null Response when the identity token is invalid.
    /// Returns a non-null Error when the token is valid but email is missing on first sign-in.</summary>
    public async Task<(AuthResponse? Response, string? Error)> AppleSignInAsync(string identityToken, string? email)
    {
        var bundleId       = config["Apple:BundleId"] ?? "com.dgit.childcare";
        var (sub, tkEmail) = await VerifyAppleTokenAsync(identityToken, bundleId);
        if (sub is null) return (null, null);

        // Apple only sends email on the first sign-in; fall back to what the client provided
        var resolvedEmail = email?.ToLower().Trim() ?? tkEmail?.ToLower();

        var user = await db.Users.FirstOrDefaultAsync(u => u.AppleId == sub)
                ?? (resolvedEmail is not null ? await db.Users.FirstOrDefaultAsync(u => u.Email == resolvedEmail) : null);

        if (user is null)
        {
            if (resolvedEmail is null)
                return (null, "Email is required for first-time Apple Sign In.");

            // Apple has already verified the email
            user = new User { Email = resolvedEmail, AppleId = sub, PasswordHash = string.Empty, EmailVerified = true };
            db.Users.Add(user);
        }
        else
        {
            if (user.AppleId is null) user.AppleId = sub;
            if (!user.EmailVerified) user.EmailVerified = true;
        }

        var refreshToken = AddRefreshToken(user);
        await db.SaveChangesAsync();
        return (BuildAuthResponse(user, refreshToken), null);
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    /// <summary>Revokes only the token for the calling device, leaving other sessions active.</summary>
    public async Task LogoutAsync(string refreshToken)
    {
        var tokenEntity = await db.UserRefreshTokens
            .FirstOrDefaultAsync(t => t.Token == refreshToken);
        if (tokenEntity is null) return;

        db.UserRefreshTokens.Remove(tokenEntity);
        await db.SaveChangesAsync();
    }

    // ── Delete account ────────────────────────────────────────────────────────

    /// <summary>Returns false when the user does not exist.</summary>
    public async Task<bool> DeleteAccountAsync(Guid userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null) return false;

        db.Users.Remove(user);  // cascades to UserRefreshTokens
        await db.SaveChangesAsync();
        return true;
    }

    // ── Email verification ────────────────────────────────────────────────────

    /// <summary>Returns false when the token is invalid or expired.</summary>
    public async Task<bool> VerifyEmailAsync(string token)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.EmailVerificationToken == token);
        if (user is null || user.EmailVerificationExpiry < DateTime.UtcNow)
            return false;

        user.EmailVerified           = true;
        user.EmailVerificationToken  = null;
        user.EmailVerificationExpiry = null;
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>Re-sends the verification email. No-op if already verified.</summary>
    public async Task ResendVerificationAsync(Guid userId)
    {
        var user = await db.Users.FindAsync(userId);
        if (user is null || user.EmailVerified) return;

        await SetVerificationTokenAsync(user);
        await db.SaveChangesAsync();
        await emailService.SendEmailVerificationAsync(user.Email, BuildVerifyUrl(user.EmailVerificationToken!));
    }

    // ── Forgot password ───────────────────────────────────────────────────────

    public async Task ForgotPasswordAsync(string email)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLower().Trim());

        // Only allow reset for accounts that have a password (not pure OAuth accounts)
        if (user is null || string.IsNullOrEmpty(user.PasswordHash)) return;

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.PasswordResetToken  = token;
        user.PasswordResetExpiry = DateTime.UtcNow.AddHours(1);
        await db.SaveChangesAsync();

        // App:ResetBaseUrl supports both deep links (childcare://reset-password)
        // and web URLs (https://yourapp.com/reset-password). Defaults to the app scheme.
        var resetBase = config["App:ResetBaseUrl"]
                     ?? $"{config["App:Scheme"] ?? "childcare"}://reset-password";
        await emailService.SendPasswordResetAsync(user.Email, $"{resetBase}?token={token}");
    }

    // ── Reset password ────────────────────────────────────────────────────────

    /// <summary>Returns false when the token is invalid or expired.</summary>
    public async Task<bool> ResetPasswordAsync(string token, string newPassword)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token);
        if (user is null || user.PasswordResetExpiry < DateTime.UtcNow) return false;

        user.PasswordHash        = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.PasswordResetToken  = null;
        user.PasswordResetExpiry = null;

        // Invalidate all active sessions across all devices
        var tokens = db.UserRefreshTokens.Where(t => t.UserId == user.Id);
        db.UserRefreshTokens.RemoveRange(tokens);

        await db.SaveChangesAsync();
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string AddRefreshToken(User user)
    {
        var entity = new UserRefreshToken
        {
            UserId    = user.Id,
            Token     = jwt.GenerateRefreshToken(),
            ExpiresAt = DateTime.UtcNow.AddDays(jwt.RefreshTokenExpiryDays),
        };
        db.UserRefreshTokens.Add(entity);
        return entity.Token;
    }

    private AuthResponse BuildAuthResponse(User user, string refreshToken) =>
        new(jwt.GenerateAccessToken(user), refreshToken, new UserDto(user.Id, user.Email, user.EmailVerified));

    private Task SetVerificationTokenAsync(User user)
    {
        user.EmailVerificationToken  = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        user.EmailVerificationExpiry = DateTime.UtcNow.AddHours(24);
        return Task.CompletedTask;
    }

    private string BuildVerifyUrl(string token)
    {
        var verifyBase = config["App:VerifyBaseUrl"]
                      ?? $"{config["App:Scheme"] ?? "childcare"}://verify-email";
        return $"{verifyBase}?token={token}";
    }

    private async Task<(string? Sub, string? Email)> VerifyAppleTokenAsync(string identityToken, string bundleId)
    {
        try
        {
            var http     = httpClientFactory.CreateClient();
            var jwksJson = await http.GetStringAsync("https://appleid.apple.com/auth/keys");
            var jwks     = new JsonWebKeySet(jwksJson);

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(identityToken, new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidIssuer              = "https://appleid.apple.com",
                ValidateAudience         = true,
                ValidAudience            = bundleId,
                ValidateLifetime         = true,
                IssuerSigningKeys        = jwks.Keys,
                ValidateIssuerSigningKey = true,
            });

            if (!result.IsValid) return (null, null);

            result.Claims.TryGetValue("sub",   out var sub);
            result.Claims.TryGetValue("email", out var email);
            return (sub?.ToString(), email?.ToString());
        }
        catch { return (null, null); }
    }
}

// ── Internal DTO (Google token payload) ───────────────────────────────────────

internal record GoogleTokenInfo(
    [property: JsonPropertyName("sub")]            string  Sub,
    [property: JsonPropertyName("email")]          string? Email,
    [property: JsonPropertyName("email_verified")] string? EmailVerified,
    [property: JsonPropertyName("aud")]            string? Aud);
