using ChildCare.Application.Common;
using ChildCare.Application.Payments;
using ChildCare.Domain.Enums;
using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChildCare.Api.Cli;

/// <summary>
/// The `send-payment-reminders` CLI subcommand body (feature 014a-invoice-payments-plus,
/// research.md R4). Mirrors MigrateTenantsCommand/BackfillGrowthCheckCommand's tenant-loop
/// structure exactly, including per-tenant failure isolation (spec.md Technical Requirements —
/// one organisation's failure must not block the rest). Triggered daily by a Cloud Scheduler +
/// Cloud Run Job execution of this container (infra/gcp/payment-reminders-scheduler.tf) —
/// this codebase's first scheduled-job entrypoint.
/// </summary>
public static class SendPaymentRemindersCommand
{
    /// <summary>Returns the process exit code: 0 if every tenant succeeded, 1 if any failed.</summary>
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        var publicDb = services.GetRequiredService<PublicDbContext>();
        var resolver = services.GetRequiredService<ITenantDbContextResolver>();
        var notificationService = services.GetRequiredService<PaymentReminderNotificationService>();

        var tenants = await publicDb.Tenants
            .Where(t => t.ProvisioningStatus == ProvisioningStatus.Ready)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        var failureCount = 0;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var tenant in tenants)
        {
            try
            {
                var db = resolver.ForSchema(tenant.SchemaName);
                var remindersSent = await ProcessTenantAsync(db, notificationService, today);
                Console.WriteLine($"{tenant.Slug}: {remindersSent} reminder(s) sent");
            }
            catch (Exception ex)
            {
                failureCount++;
                Console.WriteLine($"{tenant.Slug}: failed — {ex.Message}");
            }
        }

        Console.WriteLine($"Summary: {tenants.Count - failureCount}/{tenants.Count} tenants succeeded.");

        return failureCount == 0 ? 0 : 1;
    }

    private static async Task<int> ProcessTenantAsync(
        ITenantDbContext db, PaymentReminderNotificationService notificationService, DateOnly today)
    {
        // spec.md FR-013: Sent + overdue + reminders-enabled-location + under the 3-reminder
        // cap + due (delay/cadence) — evaluated per invoice against its own location's settings.
        var candidates = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Sent && i.DueDate != null && i.ReminderCount < 3)
            .Join(db.Locations.Where(l => l.PaymentRemindersEnabled),
                i => i.LocationId, l => l.Id,
                (i, l) => new { Invoice = i, l.PaymentReminderDelayDays, l.PaymentReminderCadenceDays })
            .ToListAsync();

        var sentCount = 0;

        foreach (var candidate in candidates)
        {
            var invoice = candidate.Invoice;
            var dueDate = invoice.DueDate!.Value;

            var nextEligibleDate = invoice.LastReminderSentAt is null
                ? dueDate.AddDays(candidate.PaymentReminderDelayDays)
                : DateOnly.FromDateTime(invoice.LastReminderSentAt.Value).AddDays(candidate.PaymentReminderCadenceDays);

            if (today < nextEligibleDate)
                continue;

            await notificationService.NotifyAsync(db, invoice);

            invoice.ReminderCount++;
            invoice.LastReminderSentAt = DateTime.UtcNow;
            invoice.UpdatedAt = DateTime.UtcNow;
            sentCount++;
        }

        if (sentCount > 0)
            await db.SaveChangesAsync();

        return sentCount;
    }
}
