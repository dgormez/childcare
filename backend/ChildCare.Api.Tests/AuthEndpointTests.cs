using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChildCare.Api.Endpoints;
using ChildCare.Api.Models;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// Migrated from ChildCareWebAppFactory (InMemory) to OrganisationOnboardingWebAppFactory (real
/// TestContainers Postgres) — AuthEndpoints now runs against TenantUser/TenantDbContext instead
/// of the deleted AppDbContext/Models.User (research.md R4, R7, tasks.md T022). The factory
/// seeds one Ready tenant so AuthService's pre-auth default-tenant shim has somewhere to
/// resolve against.
/// </summary>
public class AuthEndpointTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string UniqueEmail() => $"user_{Guid.NewGuid():N}@test.com";

    private async Task<AuthResponse> RegisterAsync(string email, string password = "password123")
    {
        var res = await _client.PostAsJsonAsync("/api/auth/register", new { email, password });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private static string? GetClaim(string accessToken, string claimType) =>
        new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims
            .FirstOrDefault(c => c.Type == claimType)?.Value;

    // ── Register ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidCredentials_Returns200WithTokens()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = UniqueEmail(), password = "password123" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body.AccessToken);
        Assert.NotEmpty(body.RefreshToken);
        Assert.NotEqual(Guid.Empty, body.User.Id);
    }

    [Fact]
    public async Task Register_ValidCredentials_IssuedTokenCarriesTenantIdClaim()
    {
        // research.md R7: every legacy-auth JWT must carry a real tenant_id claim, pointing at
        // the shim's default tenant, so the non-exempt post-login routes resolve normally.
        var auth = await RegisterAsync(UniqueEmail());

        var tenantId = GetClaim(auth.AccessToken, "tenant_id");
        Assert.False(string.IsNullOrEmpty(tenantId));
        Assert.True(Guid.TryParse(tenantId, out var parsed));
        Assert.NotEqual(Guid.Empty, parsed);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var res = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "password123" });

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithTokens()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var res = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "password123" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body.AccessToken);
        Assert.NotEmpty(body.RefreshToken);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var res = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = UniqueEmail(), password = "password123" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_ValidToken_Returns200WithRotatedTokens()
    {
        var auth = await RegisterAsync(UniqueEmail());

        var res = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = auth.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body.AccessToken);
        Assert.NotEqual(auth.RefreshToken, body.RefreshToken);
    }

    [Fact]
    public async Task Refresh_InvalidToken_Returns401()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Refresh_ConsumedToken_Returns401()
    {
        // Each refresh token is single-use; using it twice must fail
        var auth = await RegisterAsync(UniqueEmail());
        await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken });

        var res = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = auth.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_Authenticated_Returns204AndInvalidatesRefreshToken()
    {
        var auth = await RegisterAsync(UniqueEmail());
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var res = await _client.PostAsJsonAsync("/api/auth/logout",
            new { refreshToken = auth.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        _client.DefaultRequestHeaders.Authorization = null;

        // The revoked refresh token must no longer work
        var refreshRes = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshRes.StatusCode);
    }

    // ── Delete account ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAccount_Authenticated_Returns204AndPreventsLogin()
    {
        var email = UniqueEmail();
        var auth  = await RegisterAsync(email);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var deleteRes = await _client.DeleteAsync("/api/auth/account");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        _client.DefaultRequestHeaders.Authorization = null;

        var loginRes = await _client.PostAsJsonAsync("/api/auth/login",
            new { email, password = "password123" });
        Assert.Equal(HttpStatusCode.Unauthorized, loginRes.StatusCode);
    }

    // ── Email verification ────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_InvalidToken_Returns400()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/verify-email",
            new { token = "not-a-real-token" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task ResendVerification_Authenticated_Returns200()
    {
        var auth = await RegisterAsync(UniqueEmail());
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var res = await _client.PostAsync("/api/auth/resend-verification", null);

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    // ── Forgot / reset password ───────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_AnyEmail_AlwaysReturns200()
    {
        // Always 200 regardless of whether the email exists (prevents user enumeration)
        var res = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new { email = "nonexistent@example.com" });

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_Returns400()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/reset-password",
            new { token = "not-a-real-token", newPassword = "newpassword123" });

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
