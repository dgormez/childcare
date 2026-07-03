using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ChildCare.Infrastructure.Persistence;

/// <summary>Design-time only — used by `dotnet ef migrations add` / `database update`.</summary>
public class PublicDbContextFactory : IDesignTimeDbContextFactory<PublicDbContext>
{
    public PublicDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CHILDCARE_DESIGN_TIME_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=childcaredb;Username=childcare;Password=childcare;";

        var optionsBuilder = new DbContextOptionsBuilder<PublicDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new PublicDbContext(optionsBuilder.Options);
    }
}
