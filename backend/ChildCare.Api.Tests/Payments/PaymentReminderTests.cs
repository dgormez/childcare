using System.Net;
using System.Net.Http.Json;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ChildCare.Api.Tests.ChildEventTestSupport;
using static ChildCare.Api.Tests.KioskModeTestSupport;
using static ChildCare.Api.Tests.ParentTestSupport;

namespace ChildCare.Api.Tests.Payments;

/// <summary>
/// Feature 014a — spec.md User Story 3 (automatic payment reminders). FR-012 (settings,
/// defaults), FR-013 (cadence/cap/enabled-gate), FR-014 (dedicated notification copy, push
/// dispatch), Technical Requirements (per-tenant failure isolation).
/// </summary>
public class PaymentReminderTests(OrganisationOnboardingWebAppFactory factory) : IClassFixture<OrganisationOnboardingWebAppFactory>
{
    private static async Task BackdateDueDateAsync(OrganisationOnboardingWebAppFactory factory, string tenantSlug, Guid invoiceId, int daysAgo)
    {
        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
        var tenant = publicDb.Tenants.Single(t => t.Slug == tenantSlug);
        var resolver = scope.ServiceProvider.GetRequiredService<ChildCare.Application.Common.ITenantDbContextResolver>();
        var db = resolver.ForSchema(tenant.SchemaName);
        var invoice = await db.Invoices.FirstAsync(i => i.Id == invoiceId);
        invoice.DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-daysAgo));
        await db.SaveChangesAsync();
    }

    private static async Task EnableRemindersAsync(HttpClient client, string accessToken, Guid locationId, bool enabled = true, int delayDays = 3, int cadenceDays = 7) =>
        await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{locationId}/payment-reminder-settings", accessToken,
            new UpdateLocationPaymentReminderSettingsRequest(enabled, delayDays, cadenceDays)));

    [Fact]
    public async Task ReminderSettings_Defaults_AreDisabledWithSensibleValues()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Reminder Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");

        Assert.False(location.PaymentRemindersEnabled);
        Assert.Equal(3, location.PaymentReminderDelayDays);
        Assert.Equal(7, location.PaymentReminderCadenceDays);
    }

    [Fact]
    public async Task ReminderSettings_CanBeUpdated()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Reminder Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");

        var response = await client.SendAsync(AuthedRequest(
            HttpMethod.Put, $"/api/locations/{location.Id}/payment-reminder-settings", org.AccessToken,
            new UpdateLocationPaymentReminderSettingsRequest(true, 5, 10)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<LocationResponse>())!;
        Assert.True(updated.PaymentRemindersEnabled);
        Assert.Equal(5, updated.PaymentReminderDelayDays);
        Assert.Equal(10, updated.PaymentReminderCadenceDays);
    }

    [Fact]
    public async Task ReminderJob_OverdueInvoiceAtEnabledLocation_SendsExactlyOneReminder()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Reminder Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await EnableRemindersAsync(client, org.AccessToken, location.Id);
        var (child, _, _) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var invoiceRequest = new CreateContractRequest(
            location.Id, new DateOnly(2027, 1, 1), null,
            [new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0))], 3500, null);
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/contracts", org.AccessToken, invoiceRequest));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", org.AccessToken));
        var generateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{location.Id}/invoices/generate", org.AccessToken, new GenerateInvoicesRequest(2027, 1)));
        var invoice = (await generateResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!.Single(i => i.ChildId == child.Id);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([invoice.Id])));

        await BackdateDueDateAsync(factory, org.Organisation.Slug, invoice.Id, daysAgo: 10);

        var exitCode1 = await ChildCare.Api.Cli.SendPaymentRemindersCommand.RunAsync(factory.Services);
        Assert.Equal(0, exitCode1);

        var afterFirstRun = await client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/invoices/{invoice.Id}", org.AccessToken));
        var invoiceAfterFirst = (await afterFirstRun.Content.ReadFromJsonAsync<InvoiceResponse>())!;

        // Running again immediately (cadence not yet elapsed) must not send a second reminder —
        // verified indirectly via the notification-centre count staying stable, since
        // InvoiceResponse doesn't expose reminderCount directly; assert via the DB instead.
        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
            var tenant = publicDb.Tenants.Single(t => t.Slug == org.Organisation.Slug);
            var resolver = scope.ServiceProvider.GetRequiredService<ChildCare.Application.Common.ITenantDbContextResolver>();
            var db = resolver.ForSchema(tenant.SchemaName);
            var reloaded = await db.Invoices.FirstAsync(i => i.Id == invoice.Id);
            Assert.Equal(1, reloaded.ReminderCount);
            Assert.NotNull(reloaded.LastReminderSentAt);
        }

        var exitCode2 = await ChildCare.Api.Cli.SendPaymentRemindersCommand.RunAsync(factory.Services);
        Assert.Equal(0, exitCode2);

        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
            var tenant = publicDb.Tenants.Single(t => t.Slug == org.Organisation.Slug);
            var resolver = scope.ServiceProvider.GetRequiredService<ChildCare.Application.Common.ITenantDbContextResolver>();
            var db = resolver.ForSchema(tenant.SchemaName);
            var reloaded = await db.Invoices.FirstAsync(i => i.Id == invoice.Id);
            Assert.Equal(1, reloaded.ReminderCount); // still 1 — cadence hasn't elapsed
        }
    }

    [Fact]
    public async Task ReminderJob_LocationWithRemindersDisabled_SendsNoReminder()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Reminder Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main"); // reminders disabled by default
        var (child, _, _) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);

        var invoiceRequest = new CreateContractRequest(
            location.Id, new DateOnly(2027, 2, 1), null,
            [new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0))], 3500, null);
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/contracts", org.AccessToken, invoiceRequest));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", org.AccessToken));
        var generateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{location.Id}/invoices/generate", org.AccessToken, new GenerateInvoicesRequest(2027, 2)));
        var invoice = (await generateResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!.Single(i => i.ChildId == child.Id);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([invoice.Id])));
        await BackdateDueDateAsync(factory, org.Organisation.Slug, invoice.Id, daysAgo: 10);

        await ChildCare.Api.Cli.SendPaymentRemindersCommand.RunAsync(factory.Services);

        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
        var tenant = publicDb.Tenants.Single(t => t.Slug == org.Organisation.Slug);
        var resolver = scope.ServiceProvider.GetRequiredService<ChildCare.Application.Common.ITenantDbContextResolver>();
        var db = resolver.ForSchema(tenant.SchemaName);
        var reloaded = await db.Invoices.FirstAsync(i => i.Id == invoice.Id);
        Assert.Equal(0, reloaded.ReminderCount);
    }

    [Fact]
    public async Task ReminderJob_SendsPushToContactWithToken()
    {
        var client = factory.CreateClient();
        var org = await RegisterOrgAsync(client, $"Reminder Push Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var location = await CreateLocationAsync(client, org.AccessToken, "Main");
        await EnableRemindersAsync(client, org.AccessToken, location.Id);
        var (child, contact, _) = await InviteAndLoginParentAsync(client, factory, org.Organisation.Slug, org.AccessToken);
        await SetContactPushTokenAsync(org.Organisation.Slug, contact.Id, "ExponentPushToken[reminder-test]");

        var invoiceRequest = new CreateContractRequest(
            location.Id, new DateOnly(2027, 3, 1), null,
            [new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0))], 3500, null);
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{child.Id}/contracts", org.AccessToken, invoiceRequest));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", org.AccessToken));
        var generateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{location.Id}/invoices/generate", org.AccessToken, new GenerateInvoicesRequest(2027, 3)));
        var invoice = (await generateResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!.Single(i => i.ChildId == child.Id);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", org.AccessToken, new SendInvoicesRequest([invoice.Id])));
        await BackdateDueDateAsync(factory, org.Organisation.Slug, invoice.Id, daysAgo: 10);

        var pushSender = factory.Services.GetRequiredService<FakeExpoPushSender>();
        await ChildCare.Api.Cli.SendPaymentRemindersCommand.RunAsync(factory.Services);

        Assert.Contains(pushSender.Sent, p => p.PushToken == "ExponentPushToken[reminder-test]");
    }

    [Fact]
    public async Task ReminderJob_OneTenantFailing_StillProcessesOtherTenants()
    {
        var client = factory.CreateClient();

        // A healthy organisation with an eligible reminder.
        var healthyOrg = await RegisterOrgAsync(client, $"Healthy Reminder Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        var healthyLocation = await CreateLocationAsync(client, healthyOrg.AccessToken, "Main");
        await EnableRemindersAsync(client, healthyOrg.AccessToken, healthyLocation.Id);
        var (healthyChild, _, _) = await InviteAndLoginParentAsync(client, factory, healthyOrg.Organisation.Slug, healthyOrg.AccessToken);
        var invoiceRequest = new CreateContractRequest(
            healthyLocation.Id, new DateOnly(2027, 4, 1), null,
            [new ContractedDayRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0))], 3500, null);
        var createResponse = await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/children/{healthyChild.Id}/contracts", healthyOrg.AccessToken, invoiceRequest));
        var contract = (await createResponse.Content.ReadFromJsonAsync<ContractResponse>())!;
        await client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/contracts/{contract.Id}/activate", healthyOrg.AccessToken));
        var generateResponse = await client.SendAsync(AuthedRequest(
            HttpMethod.Post, $"/api/locations/{healthyLocation.Id}/invoices/generate", healthyOrg.AccessToken, new GenerateInvoicesRequest(2027, 4)));
        var healthyInvoice = (await generateResponse.Content.ReadFromJsonAsync<List<InvoiceResponse>>())!.Single(i => i.ChildId == healthyChild.Id);
        await client.SendAsync(AuthedRequest(HttpMethod.Post, "/api/invoices/send", healthyOrg.AccessToken, new SendInvoicesRequest([healthyInvoice.Id])));
        await BackdateDueDateAsync(factory, healthyOrg.Organisation.Slug, healthyInvoice.Id, daysAgo: 10);

        // A second organisation whose schema is deliberately broken (mirrors
        // BackfillGrowthCheckCommandTests' pattern of dropping a table this command reads).
        var brokenOrg = await RegisterOrgAsync(client, $"Broken Reminder Org {Guid.NewGuid():N}", $"director_{Guid.NewGuid():N}@test.com");
        using (var scope = factory.Services.CreateScope())
        {
            var publicDb = scope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
            var brokenTenant = publicDb.Tenants.Single(t => t.Slug == brokenOrg.Organisation.Slug);
            await publicDb.Database.ExecuteSqlRawAsync($"""DROP TABLE "{brokenTenant.SchemaName}"."invoices";""");
        }

        try
        {
            var exitCode = await ChildCare.Api.Cli.SendPaymentRemindersCommand.RunAsync(factory.Services);

            Assert.Equal(1, exitCode); // the broken tenant's failure is reported...

            using var healthyScope = factory.Services.CreateScope();
            var healthyPublicDb = healthyScope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
            var healthyTenant = healthyPublicDb.Tenants.Single(t => t.Slug == healthyOrg.Organisation.Slug);
            var resolver = healthyScope.ServiceProvider.GetRequiredService<ChildCare.Application.Common.ITenantDbContextResolver>();
            var healthyDb = resolver.ForSchema(healthyTenant.SchemaName);
            var reloadedHealthyInvoice = await healthyDb.Invoices.FirstAsync(i => i.Id == healthyInvoice.Id);
            Assert.Equal(1, reloadedHealthyInvoice.ReminderCount); // ...but the healthy tenant still got processed
        }
        finally
        {
            // This test host's factory/database is shared across every test in this class
            // (IClassFixture) — the deliberately-broken schema above would otherwise fail
            // every subsequent RunAsync call in the whole class. Exclude it from future runs
            // the same way a real broken tenant would eventually be handled operationally.
            using var cleanupScope = factory.Services.CreateScope();
            var cleanupPublicDb = cleanupScope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
            var brokenTenantToDisable = await cleanupPublicDb.Tenants.SingleAsync(t => t.Slug == brokenOrg.Organisation.Slug);
            brokenTenantToDisable.ProvisioningStatus = ChildCare.Domain.Enums.ProvisioningStatus.Failed;
            await cleanupPublicDb.SaveChangesAsync();
        }
    }

    private async Task SetContactPushTokenAsync(string tenantSlug, Guid contactId, string pushToken)
    {
        using var scope = factory.Services.CreateScope();
        var publicDb = scope.ServiceProvider.GetRequiredService<ChildCare.Infrastructure.Persistence.PublicDbContext>();
        var tenant = publicDb.Tenants.Single(t => t.Slug == tenantSlug);
        var resolver = scope.ServiceProvider.GetRequiredService<ChildCare.Application.Common.ITenantDbContextResolver>();
        var db = resolver.ForSchema(tenant.SchemaName);
        var contact = await db.Contacts.SingleAsync(c => c.Id == contactId);
        contact.PushToken = pushToken;
        await db.SaveChangesAsync();
    }
}
