using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ChildCare.Application.Common;
using Microsoft.Extensions.Configuration;

namespace ChildCare.Infrastructure.Auth;

/// <summary>Moved verbatim from AuthService.GoogleSignInAsync's validation logic (research.md R7).</summary>
public class GoogleTokenValidator(IHttpClientFactory httpClientFactory, IConfiguration config) : IGoogleTokenValidator
{
    public async Task<GoogleIdentity?> ValidateAsync(string idToken)
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

        return new GoogleIdentity(payload.Sub, payload.Email.ToLowerInvariant());
    }
}

internal record GoogleTokenInfo(
    [property: JsonPropertyName("sub")]            string  Sub,
    [property: JsonPropertyName("email")]          string? Email,
    [property: JsonPropertyName("email_verified")] string? EmailVerified,
    [property: JsonPropertyName("aud")]            string? Aud);
