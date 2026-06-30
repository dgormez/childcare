using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ChildCare.Api.Data;

namespace ChildCare.Api.Tests;

public class ChildCareWebAppFactory : WebApplicationFactory<Program>
{
    private const string TestJwtSecret = "test-secret-key-that-is-32-chars-long!!";

    // DbContextOptions<T> is scoped, so each HTTP request would get a fresh InMemoryDatabaseRoot
    // (and thus an empty DB) unless we share the root explicitly across the whole factory lifetime.
    private readonly InMemoryDatabaseRoot _dbRoot = new();
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"]                          = TestJwtSecret,
                ["Jwt:Issuer"]                          = "ChildCare",
                ["Jwt:Audience"]                        = "ChildCareApp",
                ["Jwt:AccessTokenExpiryMinutes"]        = "15",
                ["Jwt:RefreshTokenExpiryDays"]          = "30",
                ["ConnectionStrings:DefaultConnection"] = "DataSource=:memory:",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Program.cs skips Npgsql registration in "Testing" env, so we just add InMemory here.
            // Passing _dbRoot explicitly ensures all request scopes within this factory instance
            // share the same in-memory store (without it, each scope gets a fresh empty database).
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName, _dbRoot));

            // JWT middleware captures the signing key at Program.cs startup time, BEFORE
            // ConfigureAppConfiguration overrides take effect. PostConfigure forces the
            // middleware to use the same test secret that JwtService uses at runtime.
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
