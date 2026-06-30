using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ChildCare.Api.Models;
using ChildCare.Api.Services;

namespace ChildCare.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest req, AuthService auth) =>
        {
            var (response, emailExists) = await auth.RegisterAsync(req.Email, req.Password);
            if (emailExists) return Results.Conflict(new { error = "An account with this email already exists." });
            return Results.Ok(response);
        }).RequireRateLimiting("auth-strict");

        group.MapPost("/login", async (LoginRequest req, AuthService auth) =>
        {
            var response = await auth.LoginAsync(req.Email, req.Password);
            return response is null ? Results.Unauthorized() : Results.Ok(response);
        }).RequireRateLimiting("auth-strict");

        group.MapPost("/refresh", async (RefreshRequest req, AuthService auth) =>
        {
            var response = await auth.RefreshAsync(req.RefreshToken);
            return response is null ? Results.Unauthorized() : Results.Ok(response);
        }).RequireRateLimiting("auth-refresh");

        group.MapPost("/google", async (GoogleAuthRequest req, AuthService auth) =>
        {
            var response = await auth.GoogleSignInAsync(req.IdToken);
            return response is null ? Results.Unauthorized() : Results.Ok(response);
        }).RequireRateLimiting("auth-oauth");

        group.MapPost("/apple", async (AppleAuthRequest req, AuthService auth) =>
        {
            var (response, error) = await auth.AppleSignInAsync(req.IdentityToken, req.Email);
            if (error is not null) return Results.BadRequest(new { error });
            return response is null ? Results.Unauthorized() : Results.Ok(response);
        }).RequireRateLimiting("auth-oauth");

        // Revokes only the calling device's session; other devices stay logged in
        group.MapPost("/logout", async (LogoutRequest req, AuthService auth) =>
        {
            await auth.LogoutAsync(req.RefreshToken);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapDelete("/account", async (HttpContext ctx, AuthService auth) =>
        {
            var userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();
            var deleted = await auth.DeleteAccountAsync(Guid.Parse(userId));
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization();

        group.MapPost("/verify-email", async (VerifyEmailRequest req, AuthService auth) =>
        {
            var success = await auth.VerifyEmailAsync(req.Token);
            return success
                ? Results.Ok(new { message = "Email verified." })
                : Results.BadRequest(new { error = "This verification link is invalid or has expired." });
        }).RequireRateLimiting("auth-strict");

        group.MapPost("/resend-verification", async (HttpContext ctx, AuthService auth) =>
        {
            var userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();
            await auth.ResendVerificationAsync(Guid.Parse(userId));
            return Results.Ok(new { message = "If your email is unverified, a new link has been sent." });
        }).RequireAuthorization().RequireRateLimiting("auth-strict");

        group.MapPost("/forgot-password", async (ForgotPasswordRequest req, AuthService auth) =>
        {
            await auth.ForgotPasswordAsync(req.Email);
            return Results.Ok(new { message = "If that email is registered, a reset link has been sent." });
        }).RequireRateLimiting("auth-strict");

        group.MapPost("/reset-password", async (ResetPasswordRequest req, AuthService auth) =>
        {
            var success = await auth.ResetPasswordAsync(req.Token, req.NewPassword);
            return success
                ? Results.Ok(new { message = "Password has been reset. You can now sign in." })
                : Results.BadRequest(new { error = "This reset link is invalid or has expired." });
        }).RequireRateLimiting("auth-strict");
    }
}

// ── Request / Response DTOs ───────────────────────────────────────────────────

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(8)]                 string Password);

public record LoginRequest(
    [Required] string Email,
    [Required] string Password);

public record RefreshRequest([Required] string RefreshToken);

public record LogoutRequest([Required] string RefreshToken);

public record GoogleAuthRequest([Required] string IdToken);

public record AppleAuthRequest([Required] string IdentityToken, string? Email);

public record VerifyEmailRequest([Required] string Token);

public record ForgotPasswordRequest([Required, EmailAddress] string Email);

public record ResetPasswordRequest([Required] string Token, [Required, MinLength(8)] string NewPassword);
