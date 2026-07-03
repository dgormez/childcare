using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ChildCare.Api.Auth;

public class SuperAdminAuthenticationOptions : AuthenticationSchemeOptions;

/// <summary>
/// Gates admin-only endpoints behind a static shared-secret header, sourced from Secret
/// Manager in deployed environments (research.md R11). An explicitly temporary Phase 1
/// measure — replaced by proper super-admin auth once an admin UI exists (Phase 2). Implemented
/// as an authentication scheme (rather than a manually-invoked check in each endpoint) so it
/// composes with [Authorize]/RequireAuthorization and shows up in OpenAPI security metadata,
/// the same way every other auth requirement in the app does.
/// </summary>
public class SuperAdminAuthenticationHandler(
    IOptionsMonitor<SuperAdminAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<SuperAdminAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "SuperAdmin";
    public const string HeaderName = "X-Superadmin-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expected = configuration["SuperAdmin:ApiKey"];
        if (string.IsNullOrEmpty(expected))
            return Task.FromResult(AuthenticateResult.Fail("Super-admin API key is not configured."));

        if (!Request.Headers.TryGetValue(HeaderName, out var provided) || provided.Count == 0)
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!FixedTimeEquals(provided.ToString(), expected))
            return Task.FromResult(AuthenticateResult.Fail("Invalid super-admin API key."));

        var identity = new ClaimsIdentity(SchemeName);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        return Response.WriteAsJsonAsync(new { errorKey = "errors.unauthorized" });
    }

    private static bool FixedTimeEquals(string a, string b)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
