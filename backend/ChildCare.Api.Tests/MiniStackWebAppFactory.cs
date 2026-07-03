using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ChildCare.Api.Data;

namespace ChildCare.Api.Tests;

public class ChildCareWebAppFactory : TestWebAppFactoryBase
{
    // DbContextOptions<T> is scoped, so each HTTP request would get a fresh InMemoryDatabaseRoot
    // (and thus an empty DB) unless we share the root explicitly across the whole factory lifetime.
    private readonly InMemoryDatabaseRoot _dbRoot = new();
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
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
        });
    }
}
