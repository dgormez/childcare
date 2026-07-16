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

    // Seeded once per factory instance (i.e. once per test class, since IClassFixture shares
    // this instance) so every consumer of this factory has at least one Ready tenant to work
    // with without repeating onboarding boilerplate first. Feature 002/003's default-tenant
    // shim this used to backstop (research.md R7) is gone — real slug-based resolution
    // (feature 003, research.md R1) is now unconditional.
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
                ["App:ApiBaseUrl"]                       = "https://test.childcare.local",
            });
        });

        builder.ConfigureLogging(logging => logging.AddProvider(LogCapture));

        builder.ConfigureServices(services =>
        {
            // Program.cs skips Npgsql registration for PublicDbContext in "Testing" env.
            services.AddDbContext<PublicDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
            services.AddScoped<IPublicDbContext>(sp => sp.GetRequiredService<PublicDbContext>());

            // Overrides Program.cs's real (Scoped) Google/Apple validators with Singleton fakes
            // (research.md R7) — the last registration wins for a non-collection service, and
            // Singleton means factory.Services.GetRequiredService<FakeGoogleTokenValidator>()
            // resolves the exact instance a request will use, mirroring TenantMiddleware's
            // FailureInjectionHookForTests seam. Harmless for test classes that never call
            // /api/auth/google or /api/auth/apple — the fakes are simply never invoked.
            services.AddSingleton<FakeGoogleTokenValidator>();
            services.AddSingleton<IGoogleTokenValidator>(sp => sp.GetRequiredService<FakeGoogleTokenValidator>());
            services.AddSingleton<FakeAppleTokenValidator>();
            services.AddSingleton<IAppleTokenValidator>(sp => sp.GetRequiredService<FakeAppleTokenValidator>());

            services.AddSingleton<FakeProfilePhotoStorage>();
            services.AddSingleton<IProfilePhotoStorage>(sp => sp.GetRequiredService<FakeProfilePhotoStorage>());

            services.AddSingleton<FakeGroupActivityPhotoStorage>();
            services.AddSingleton<IGroupActivityPhotoStorage>(sp => sp.GetRequiredService<FakeGroupActivityPhotoStorage>());

            services.AddSingleton<FakeHealthAttachmentStorage>();
            services.AddSingleton<IHealthAttachmentStorage>(sp => sp.GetRequiredService<FakeHealthAttachmentStorage>());

            services.AddSingleton<FakeFiscalAttestationStorage>();
            services.AddSingleton<IFiscalAttestationStorage>(sp => sp.GetRequiredService<FakeFiscalAttestationStorage>());

            // Wraps a real EmailService (manually constructed with Singleton-safe dependencies
            // — IConfiguration/ILogger, no per-request state) so tests get real dev-mode
            // log-and-return behavior by default, but can flip ThrowOnStaffInvitation to prove a
            // send failure doesn't fail the caller (feature 005-staff, /speckit-converge F1).
            services.AddSingleton<FakeEmailSender>(sp =>
            {
                var innerEmailService = new ChildCare.Api.Services.EmailService(
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<ILogger<ChildCare.Api.Services.EmailService>>());
                return new FakeEmailSender(innerEmailService);
            });
            services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<FakeEmailSender>());

            // Feature 009 — never call the real Expo push service in tests.
            services.AddSingleton<FakeExpoPushSender>();
            services.AddSingleton<IExpoPushSender>(sp => sp.GetRequiredService<FakeExpoPushSender>());

            // Feature 014a — never call the real Mollie API in tests.
            services.AddSingleton<FakePaymentProvider>();
            services.AddSingleton<IPaymentProvider>(sp => sp.GetRequiredService<FakePaymentProvider>());
        });
    }
}
