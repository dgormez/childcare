using System.Security.Claims;
using ChildCare.Api.Middleware;
using ChildCare.Application.Auth;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // login/google/apple/refresh/forgot-password/reset-password/verify-email all run before
        // a tenant-bearing session exists, so they're exempt from TenantMiddleware and resolve
        // their own schema from a client-supplied organisation slug instead (research.md R1,
        // replacing feature 002's default-tenant shim). There is no open self-registration
        // endpoint (FR-009) — every account is created by an invitation-based provisioning flow
        // (organisation onboarding for directors; features 005/006/012 for staff/parents).

        group.MapPost("/login", async (LoginRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new LoginCommand(req.OrganisationSlug, req.Email, req.Password));
            return result.Succeeded ? Results.Ok(result.Response) : MapAuthFailure(result.Failure!.Value);
        }).RequireRateLimiting("auth-strict").RequireTenantExempt();

        group.MapPost("/refresh", async (RefreshRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new RefreshTokenCommand(req.OrganisationSlug, req.RefreshToken));
            return result.Succeeded ? Results.Ok(result.Response) : MapAuthFailure(result.Failure!.Value);
        }).RequireRateLimiting("auth-refresh").RequireTenantExempt();

        group.MapPost("/google", async (GoogleAuthRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new GoogleSignInCommand(req.OrganisationSlug, req.IdToken));
            return result.Succeeded ? Results.Ok(result.Response) : MapAuthFailure(result.Failure!.Value);
        }).RequireRateLimiting("auth-oauth").RequireTenantExempt();

        group.MapPost("/apple", async (AppleAuthRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new AppleSignInCommand(req.OrganisationSlug, req.IdentityToken, req.Email));
            return result.Succeeded ? Results.Ok(result.Response) : MapAuthFailure(result.Failure!.Value);
        }).RequireRateLimiting("auth-oauth").RequireTenantExempt();

        // Revokes only the calling device's session; other devices stay logged in
        group.MapPost("/logout", async (LogoutRequest req, IMediator mediator) =>
        {
            await mediator.Send(new LogoutCommand(req.RefreshToken));
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapDelete("/account", async (HttpContext ctx, IMediator mediator) =>
        {
            var userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();
            var deleted = await mediator.Send(new DeleteAccountCommand(Guid.Parse(userId)));
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization();

        group.MapPost("/verify-email", async (VerifyEmailRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new VerifyEmailCommand(req.OrganisationSlug, req.Token));
            return result.Succeeded
                ? Results.Ok(new { message = "Email verified." })
                : MapAuthFailure(result.Failure!.Value);
        }).RequireRateLimiting("auth-strict").RequireTenantExempt();

        // Not exempt: the caller is already authenticated, so tenant context is already
        // resolved by TenantMiddleware like any other post-login endpoint.
        group.MapPost("/resend-verification", async (HttpContext ctx, IMediator mediator) =>
        {
            var userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();
            await mediator.Send(new ResendVerificationCommand(Guid.Parse(userId)));
            return Results.Ok(new { message = "If your email is unverified, a new link has been sent." });
        }).RequireAuthorization().RequireRateLimiting("auth-strict");

        group.MapPost("/forgot-password", async (ForgotPasswordRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new ForgotPasswordCommand(req.OrganisationSlug, req.Email));
            return result.Succeeded
                ? Results.Ok(new { message = "If that email is registered, a reset link has been sent." })
                : MapAuthFailure(result.Failure!.Value);
        }).RequireRateLimiting("auth-strict").RequireTenantExempt();

        group.MapPost("/reset-password", async (ResetPasswordRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new ResetPasswordCommand(req.OrganisationSlug, req.Token, req.NewPassword));
            return result.Succeeded
                ? Results.Ok(new { message = "Password has been reset. You can now sign in." })
                : MapAuthFailure(result.Failure!.Value);
        }).RequireRateLimiting("auth-strict").RequireTenantExempt();
    }

    /// <summary>
    /// Shared AuthFailure → HTTP mapping (contracts/auth-api.md, ERROR_KEYS.md) for every
    /// command built on the shared AuthResult/AuthActionResult shape — HTTP translation, not
    /// business logic (constitution Principle III).
    /// </summary>
    private static IResult MapAuthFailure(AuthFailure failure) => failure switch
    {
        AuthFailure.OrganisationNotFound => Results.Json(
            new { errorKey = "errors.auth.organisation_not_found" },
            statusCode: StatusCodes.Status404NotFound),

        AuthFailure.InvalidCredentials => Results.Json(
            new { errorKey = "errors.auth.invalid_credentials" },
            statusCode: StatusCodes.Status401Unauthorized),

        AuthFailure.MethodNotAllowedForRole => Results.Json(
            new { errorKey = "errors.auth.method_not_allowed_for_role" },
            statusCode: StatusCodes.Status403Forbidden),

        AuthFailure.TokenInvalidOrExpired => Results.Json(
            new { errorKey = "errors.auth.token_invalid_or_expired" },
            statusCode: StatusCodes.Status400BadRequest),

        AuthFailure.AppleEmailRequiredFirstSignIn => Results.Json(
            new { errorKey = "errors.auth.apple_email_required_first_signin" },
            statusCode: StatusCodes.Status400BadRequest),

        _ => throw new InvalidOperationException($"Unhandled {nameof(AuthFailure)}: {failure}"),
    };
}
