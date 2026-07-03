using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// Constitution Principle V (NON-NEGOTIABLE): tests run against a real, TestContainers-
/// provisioned PostgreSQL — schema-per-tenant behavior (CREATE SCHEMA, search_path) has no
/// InMemory equivalent. Covers both the public schema (tenants, invitations) and tenant
/// schemas (users, refresh tokens) — AppDbContext/InMemory (feature 001's placeholder for the
/// unrelated legacy walking-skeleton) is gone as of feature 002 (research.md R4).
/// </summary>
public class OrganisationOnboardingWebAppFactory : TestWebAppFactoryBase, IAsyncLifetime
{
    public const string SuperAdminApiKey = "test-superadmin-key";

    // In-memory log sink, so tests can assert something was logged server-side (tasks.md T030,
    // FR-008a) without depending on console output.
    public CapturingLoggerProvider LogCapture { get; } = new();

    // AuthService's pre-auth default-tenant shim (research.md R7) requires at least one Ready
    // tenant to exist — seeded once per factory instance (i.e. once per test class, since
    // IClassFixture shares this instance) so every consumer of this factory can exercise
    // AuthEndpoints without repeating onboarding boilerplate first.
    private const string DefaultTenantDirectorEmail = "default-tenant-director@test.com";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
            await db.Database.MigrateAsync();
        }

        await SeedDefaultTenantAsync();
    }

    async Task IAsyncLifetime.DisposeAsync() => await _postgres.DisposeAsync();

    private async Task SeedDefaultTenantAsync()
    {
        var client = CreateClient();

        var inviteRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/invitations")
        {
            Content = JsonContent.Create(new CreateInvitationRequest(DefaultTenantDirectorEmail)),
        };
        inviteRequest.Headers.Add("X-Superadmin-Key", SuperAdminApiKey);
        var inviteResponse = await client.SendAsync(inviteRequest);
        inviteResponse.EnsureSuccessStatusCode();
        var invitation = (await inviteResponse.Content.ReadFromJsonAsync<CreateInvitationResponse>())!;

        var registerResponse = await client.PostAsJsonAsync("/api/organisations/register", new RegisterOrganisationRequest(
            invitation.Token, "Default Test Org", "Default Test Director", invitation.Email, "password123"));
        registerResponse.EnsureSuccessStatusCode();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SuperAdmin:ApiKey"]                    = SuperAdminApiKey,
                ["ConnectionStrings:DefaultConnection"]  = _postgres.GetConnectionString(),
            });
        });

        builder.ConfigureLogging(logging => logging.AddProvider(LogCapture));

        builder.ConfigureServices(services =>
        {
            // Program.cs skips Npgsql registration for PublicDbContext in "Testing" env.
            services.AddDbContext<PublicDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
            services.AddScoped<IPublicDbContext>(sp => sp.GetRequiredService<PublicDbContext>());
        });
    }
}
