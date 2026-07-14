using ChildCare.Api.Cli;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>Feature 013h Foundational (tasks.md T014, research.md R3): the CLI command is the
/// only write path for TenantUser.IsPlatformAdmin (FR-001) — grants by email across every Ready
/// tenant, leaves non-matching tenants/accounts untouched, and is idempotent.</summary>
public class GrantPlatformAdminCommandTests(OrganisationOnboardingWebAppFactory factory)
    : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task RunAsync_MatchingEmail_GrantsFlag_LeavesOtherTenantsAndAccountsUntouched()
    {
        var client = factory.CreateClient();
        var targetEmail = $"director_{Guid.NewGuid():N}@test.com";
        var otherEmail = $"director_{Guid.NewGuid():N}@test.com";

        var targetOrg = await RegisterOrgAsync(client, $"Org {Guid.NewGuid():N}", targetEmail);
        var otherOrg = await RegisterOrgAsync(client, $"Org {Guid.NewGuid():N}", otherEmail);

        using (var scope = factory.Services.CreateScope())
        {
            var exitCode = await GrantPlatformAdminCommand.RunAsync(scope.ServiceProvider, targetEmail);
            Assert.Equal(0, exitCode);
        }

        Assert.True(await IsPlatformAdminAsync(targetOrg.Organisation.Id, targetEmail));
        Assert.False(await IsPlatformAdminAsync(otherOrg.Organisation.Id, otherEmail));
    }

    [Fact]
    public async Task RunAsync_RunTwice_IsIdempotent()
    {
        var client = factory.CreateClient();
        var email = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Org {Guid.NewGuid():N}", email);

        using (var scope = factory.Services.CreateScope())
        {
            Assert.Equal(0, await GrantPlatformAdminCommand.RunAsync(scope.ServiceProvider, email));
        }
        using (var scope = factory.Services.CreateScope())
        {
            Assert.Equal(0, await GrantPlatformAdminCommand.RunAsync(scope.ServiceProvider, email));
        }

        Assert.True(await IsPlatformAdminAsync(org.Organisation.Id, email));
    }

    [Fact]
    public async Task RunAsync_NoMatchingEmail_SucceedsWithoutGrantingAnyAccount()
    {
        var client = factory.CreateClient();
        var email = $"director_{Guid.NewGuid():N}@test.com";
        var org = await RegisterOrgAsync(client, $"Org {Guid.NewGuid():N}", email);

        using (var scope = factory.Services.CreateScope())
        {
            var exitCode = await GrantPlatformAdminCommand.RunAsync(scope.ServiceProvider, $"nobody_{Guid.NewGuid():N}@test.com");
            Assert.Equal(0, exitCode);
        }

        Assert.False(await IsPlatformAdminAsync(org.Organisation.Id, email));
    }

    private async Task<bool> IsPlatformAdminAsync(Guid tenantId, string email)
    {
        var schemaName = await GetSchemaNameAsync(factory.Services, tenantId);
        var tenantDb = ResolveTenantDb(factory.Services, schemaName);
        var user = await tenantDb.Users.SingleAsync(u => u.Email == email);
        return user.IsPlatformAdmin;
    }
}
