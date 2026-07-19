using ChildCare.Application.Common;
using ChildCare.Application.Email;
using ChildCare.Domain.Enums;
using ChildCare.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChildCare.Api.Cli;

/// <summary>
/// The `send-daily-reports` CLI subcommand body (feature 020, User Story 2). Mirrors
/// `SendPaymentRemindersCommand`'s tenant-loop structure exactly, including per-tenant failure
/// isolation (spec.md Technical Requirements). Triggered daily at 19:00 Europe/Brussels by a
/// Cloud Scheduler + Cloud Run Job execution of this container (infra/gcp/main.tf), mirroring
/// that feature's first scheduled-job entrypoint.
/// </summary>
public static class SendDailyReportsCommand
{
    /// <summary>Returns the process exit code: 0 if every tenant succeeded, 1 if any failed.</summary>
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        var publicDb = services.GetRequiredService<PublicDbContext>();
        var resolver = services.GetRequiredService<ITenantDbContextResolver>();
        var digestService = services.GetRequiredService<DailyReportDigestService>();

        var tenants = await publicDb.Tenants
            .Where(t => t.ProvisioningStatus == ProvisioningStatus.Ready)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        var failureCount = 0;
        var today = BelgianCalendarDay.Today();

        foreach (var tenant in tenants)
        {
            try
            {
                var db = resolver.ForSchema(tenant.SchemaName);
                var summary = await digestService.ProcessTenantAsync(db, tenant.Slug, today);
                Console.WriteLine($"{tenant.Slug}: {summary.SentCount} sent, {summary.SkippedNoEmailCount} skipped (no email)");
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
}
