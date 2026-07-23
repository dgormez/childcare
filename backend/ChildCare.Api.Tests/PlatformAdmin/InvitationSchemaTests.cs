using ChildCare.Domain.Entities;
using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>Foundational (tasks.md T008): the five new Invitation columns (data-model.md) persist
/// and round-trip correctly, including Locale's default and the "all three null or all three
/// populated" revoke-attribution invariant.</summary>
public class InvitationSchemaTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    [Fact]
    public async Task Invitation_NewColumns_RoundTripCorrectly_WithLocaleDefault()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PublicDbContext>();

        var invitation = new Invitation
        {
            Email = $"schema_{Guid.NewGuid():N}@test.com",
            TokenHash = Guid.NewGuid().ToByteArray(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            OrganisationNameNote = "Zonnebloem KDV",
        };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync();
        db.Detach(invitation);

        var reloaded = await db.Invitations.FirstAsync(i => i.Id == invitation.Id);
        Assert.Equal("nl", reloaded.Locale);
        Assert.Equal("Zonnebloem KDV", reloaded.OrganisationNameNote);
        Assert.Null(reloaded.CreatedByUserId);
        Assert.Null(reloaded.CreatedByEmail);
        Assert.Null(reloaded.RevokedByUserId);
        Assert.Null(reloaded.RevokedByEmail);
        Assert.Null(reloaded.RevokedAt);

        reloaded.CreatedByUserId = Guid.NewGuid();
        reloaded.CreatedByEmail = "admin@test.com";
        reloaded.RevokedByUserId = Guid.NewGuid();
        reloaded.RevokedByEmail = "admin2@test.com";
        reloaded.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        db.Detach(reloaded);

        var fullyPopulated = await db.Invitations.FirstAsync(i => i.Id == invitation.Id);
        Assert.NotNull(fullyPopulated.CreatedByUserId);
        Assert.NotNull(fullyPopulated.RevokedByUserId);
        Assert.NotNull(fullyPopulated.RevokedByEmail);
        Assert.NotNull(fullyPopulated.RevokedAt);
    }
}
