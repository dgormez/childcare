using ChildCare.Api.Data;
using ChildCare.Application.Common;
using ChildCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace ChildCare.Api.Tests;

/// <summary>
/// Constitution Principle V (NON-NEGOTIABLE): organisation-onboarding tests run against a
/// real, TestContainers-provisioned PostgreSQL — schema-per-tenant behavior (CREATE SCHEMA,
/// search_path) has no InMemory equivalent. AppDbContext (unrelated to this feature, research.md
/// R2) still uses InMemory here, matching the existing ChildCareWebAppFactory pattern.
/// </summary>
public class OrganisationOnboardingWebAppFactory : TestWebAppFactoryBase, IAsyncLifetime
{
    public const string SuperAdminApiKey = "test-superadmin-key";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private readonly InMemoryDatabaseRoot _appDbRoot = new();
    private readonly string _appDbName = $"TestDb_{Guid.NewGuid()}";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PublicDbContext>();
        await db.Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync() => await _postgres.DisposeAsync();

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

        builder.ConfigureServices(services =>
        {
            // Program.cs skips Npgsql registration for both contexts in "Testing" env.
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_appDbName, _appDbRoot));

            services.AddDbContext<PublicDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
            services.AddScoped<IPublicDbContext>(sp => sp.GetRequiredService<PublicDbContext>());
        });
    }
}
