using System.Net;
using System.Net.Http.Json;
using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;
using static ChildCare.Api.Tests.GroupActivities.GroupActivityTestSupport;

namespace ChildCare.Api.Tests.Cli;

/// <summary>User Story 2 (spec.md): the automatic `send-daily-reports` digest job. Assertions
/// filter FakeEmailSender.DailyReportCalls by the test's own contact/child, not by count, since
/// the shared IClassFixture factory means earlier tests' tenants get reprocessed on every
/// RunAsync call too (matches PaymentReminderTests' existing convention for the same reason).</summary>
public class SendDailyReportsCommandTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task<ContactResponse> CreateContactWithLocaleAsync(HttpClient client, string accessToken, string locale, string firstName) =>
        (await (await client.SendAsync(AuthedRequest(
            HttpMethod.Post, "/api/contacts", accessToken,
            new ChildCare.Contracts.Requests.CreateContactRequest(firstName, "Peeters", "+32 9 123 45 67", $"{Guid.NewGuid():N}@test.com", locale))))
            .Content.ReadFromJsonAsync<ContactResponse>())!;

    private static async Task SetDigestUnsubscribedAsync(IServiceProvider services, string tenantSlug, Guid contactId)
    {
        using var scope = services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
        var tenant = await publicDb.Tenants.SingleAsync(t => t.Slug == tenantSlug);
        var db = ResolveTenantDb(scope.ServiceProvider, tenant.SchemaName);
        var contact = await db.Contacts.SingleAsync(c => c.Id == contactId);
        contact.DigestUnsubscribedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // ── Scenario 1: one independent, locale-correct email per guardian contact ─────

    [Fact]
    public async Task SendDailyReports_TwoGuardiansDifferentLocales_SendsIndependentLocaleCorrectEmails()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"DigestLocale Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken, "Emma");
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));

        var nlContact = await CreateContactWithLocaleAsync(client, org.AccessToken, "nl", "Anna");
        await LinkContactAsync(client, org.AccessToken, child.Id, nlContact.Id, relationship: "Mother");
        var frContact = await CreateContactWithLocaleAsync(client, org.AccessToken, "fr", "Bram");
        await LinkContactAsync(client, org.AccessToken, child.Id, frContact.Id, relationship: "Father");

        var exitCode = await ChildCare.Api.Cli.SendDailyReportsCommand.RunAsync(factory.Services);
        Assert.Equal(0, exitCode);

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        Assert.Single(fakeEmail.DailyReportCalls, c => c.ToEmail == nlContact.Email && c.Locale == "nl");
        Assert.Single(fakeEmail.DailyReportCalls, c => c.ToEmail == frContact.Email && c.Locale == "fr");
    }

    // ── Scenario 2: a child with zero events today still receives an email ─────────

    [Fact]
    public async Task SendDailyReports_ChildWithZeroEvents_StillSendsEmailWithEmptySummary()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"DigestZeroEvents Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken, "NoEventsChild");
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        var contact = await CreateContactWithEmailAsync(client, org.AccessToken);
        await LinkContactAsync(client, org.AccessToken, child.Id, contact.Id);

        var exitCode = await ChildCare.Api.Cli.SendDailyReportsCommand.RunAsync(factory.Services);
        Assert.Equal(0, exitCode);

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        var call = Assert.Single(fakeEmail.DailyReportCalls, c => c.ToEmail == contact.Email);
        // The exact "no updates logged today" copy is a Scriban template concern (already
        // exercised by the template itself); what this test proves is the pipeline still sends
        // for a zero-event child rather than skipping it, with a summary shape that yields the
        // template's no-events branch.
        Assert.Equal(0, call.Summary.NapsCount);
        Assert.Equal(0, call.Summary.BottlesCount);
        Assert.Equal(0, call.Summary.DiaperChangesCount);
        Assert.Null(call.Summary.LatestMood);
        Assert.Null(call.Summary.LatestTemperatureCelsius);
        Assert.False(call.Summary.MedicationAdministered);
        Assert.Empty(call.Summary.Activities);
        Assert.Empty(call.Summary.GroupActivities);
    }

    // ── Scenario 3/6: a digest-unsubscribed contact is skipped, others still receive theirs ─

    [Fact]
    public async Task SendDailyReports_UnsubscribedContact_SkippedWhileOtherGuardianStillReceives()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"DigestUnsub Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var child = await CreateChildAsync(client, org.AccessToken, "SharedChild");
        await AssignChildToGroupAsync(client, org.AccessToken, child.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));

        var subscribedContact = await CreateContactWithEmailAsync(client, org.AccessToken, firstName: "Subscribed");
        await LinkContactAsync(client, org.AccessToken, child.Id, subscribedContact.Id, relationship: "Mother");
        var unsubscribedContact = await CreateContactWithEmailAsync(client, org.AccessToken, firstName: "Unsubscribed");
        await LinkContactAsync(client, org.AccessToken, child.Id, unsubscribedContact.Id, relationship: "Father");
        await SetDigestUnsubscribedAsync(factory.Services, org.Organisation.Slug, unsubscribedContact.Id);

        var exitCode = await ChildCare.Api.Cli.SendDailyReportsCommand.RunAsync(factory.Services);
        Assert.Equal(0, exitCode);

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        Assert.Contains(fakeEmail.DailyReportCalls, c => c.ToEmail == subscribedContact.Email);
        Assert.DoesNotContain(fakeEmail.DailyReportCalls, c => c.ToEmail == unsubscribedContact.Email);
    }

    // ── Per-tenant failure isolation (matches SendPaymentRemindersCommand's existing test) ──

    [Fact]
    public async Task SendDailyReports_OneTenantSchemaBroken_HealthyTenantStillProcessed()
    {
        var client = factory.CreateClient();

        var healthyOrg = await RegisterOrgAsync(client, $"DigestHealthy Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var healthyLocation = await CreateLocationAsync(client, healthyOrg.AccessToken, "Location A");
        var healthyGroup = await CreateGroupAsync(client, healthyOrg.AccessToken, "Group A", healthyLocation.Id);
        var healthyChild = await CreateChildAsync(client, healthyOrg.AccessToken, "HealthyChild");
        await AssignChildToGroupAsync(client, healthyOrg.AccessToken, healthyChild.Id, healthyGroup.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        var healthyContact = await CreateContactWithEmailAsync(client, healthyOrg.AccessToken);
        await LinkContactAsync(client, healthyOrg.AccessToken, healthyChild.Id, healthyContact.Id);

        var brokenOrg = await RegisterOrgAsync(client, $"DigestBroken Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        // Must have at least one currently-enrolled child, or ProcessTenantAsync's loop never
        // reaches the (now-dropped) Contacts query and this tenant would spuriously "succeed".
        var brokenLocation = await CreateLocationAsync(client, brokenOrg.AccessToken, "Location A");
        var brokenGroup = await CreateGroupAsync(client, brokenOrg.AccessToken, "Group A", brokenLocation.Id);
        var brokenChild = await CreateChildAsync(client, brokenOrg.AccessToken, "BrokenChild");
        await AssignChildToGroupAsync(client, brokenOrg.AccessToken, brokenChild.Id, brokenGroup.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
            var brokenTenant = publicDb.Tenants.Single(t => t.Slug == brokenOrg.Organisation.Slug);
            await publicDb.Database.ExecuteSqlRawAsync($"""DROP TABLE "{brokenTenant.SchemaName}"."contacts" CASCADE;""");
        }

        try
        {
            var exitCode = await ChildCare.Api.Cli.SendDailyReportsCommand.RunAsync(factory.Services);
            Assert.Equal(1, exitCode); // the broken tenant's failure is reported...

            var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
            Assert.Contains(fakeEmail.DailyReportCalls, c => c.ToEmail == healthyContact.Email); // ...but the healthy tenant still got processed
        }
        finally
        {
            using var cleanupScope = factory.Services.CreateScope();
            var cleanupPublicDb = cleanupScope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
            var brokenTenantToDisable = await cleanupPublicDb.Tenants.SingleAsync(t => t.Slug == brokenOrg.Organisation.Slug);
            brokenTenantToDisable.ProvisioningStatus = ChildCare.Domain.Enums.ProvisioningStatus.Failed;
            await cleanupPublicDb.SaveChangesAsync();
        }
    }

    // ── Edge case: each child's own Contract.Consent.PhotosInternal is respected independently ──

    [Fact]
    public async Task SendDailyReports_SameContactDifferingChildConsent_RespectsEachChildsOwnConsent()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"DigestConsent Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Location A");
        var group = await CreateGroupAsync(client, org.AccessToken, "Group A", location.Id);
        var (_, deviceToken) = await PairDeviceAsync(client, org.AccessToken, location.Id, group.Id);

        var contact = await CreateContactWithEmailAsync(client, org.AccessToken, firstName: "SharedParent");

        var consentingChild = await CreateChildAsync(client, org.AccessToken, "ConsentingChild");
        await AssignChildToGroupAsync(client, org.AccessToken, consentingChild.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        await CreateActiveContractAsync(client, org.AccessToken, consentingChild.Id, location.Id, photosInternal: true);
        await LinkContactAsync(client, org.AccessToken, consentingChild.Id, contact.Id);

        var nonConsentingChild = await CreateChildAsync(client, org.AccessToken, "NonConsentingChild");
        await AssignChildToGroupAsync(client, org.AccessToken, nonConsentingChild.Id, group.Id, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-60)));
        await CreateActiveContractAsync(client, org.AccessToken, nonConsentingChild.Id, location.Id, photosInternal: false);
        await LinkContactAsync(client, org.AccessToken, nonConsentingChild.Id, contact.Id);

        var activity = await CreateGroupActivityOkAsync(client, deviceToken, "outdoor", "In de tuin");
        await UploadPhotoAsync(client, deviceToken, activity.Id);

        var exitCode = await ChildCare.Api.Cli.SendDailyReportsCommand.RunAsync(factory.Services);
        Assert.Equal(0, exitCode);

        var fakeEmail = factory.Services.GetRequiredService<FakeEmailSender>();
        var consentingCall = Assert.Single(fakeEmail.DailyReportCalls, c => c.ToEmail == contact.Email && c.ChildName == "ConsentingChild");
        var nonConsentingCall = Assert.Single(fakeEmail.DailyReportCalls, c => c.ToEmail == contact.Email && c.ChildName == "NonConsentingChild");

        var consentingActivity = Assert.Single(consentingCall.Summary.GroupActivities);
        Assert.Single(consentingActivity.Photos);

        var nonConsentingActivity = Assert.Single(nonConsentingCall.Summary.GroupActivities);
        Assert.Empty(nonConsentingActivity.Photos);
    }
}
