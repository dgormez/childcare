using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ChildCare.Api.Tests;

/// <summary>
/// Program.cs's JWT middleware captures its signing key at startup time, before
/// ConfigureAppConfiguration overrides take effect, so every test factory needs the same
/// PostConfigure override to force the middleware onto the same test secret JwtService uses
/// at runtime. Shared here so every concrete factory doesn't repeat it — subclasses call
/// base.ConfigureWebHost(builder) first, then add their own DbContext/config wiring.
/// </summary>
public abstract class TestWebAppFactoryBase : WebApplicationFactory<Program>
{
    protected const string TestJwtSecret = "test-secret-key-that-is-32-chars-long!!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"]                   = TestJwtSecret,
                ["Jwt:Issuer"]                   = "ChildCare",
                ["Jwt:Audience"]                 = "ChildCareApp",
                ["Jwt:AccessTokenExpiryMinutes"] = "15",
                ["Jwt:RefreshTokenExpiryDays"]   = "30",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSecret));
                options.TokenValidationParameters.IssuerSigningKey = key;
                options.TokenValidationParameters.ValidIssuer      = "ChildCare";
                options.TokenValidationParameters.ValidAudience     = "ChildCareApp";
            });
        });
    }
}
