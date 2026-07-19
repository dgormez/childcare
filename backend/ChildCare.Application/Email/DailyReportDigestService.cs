using ChildCare.Application.ChildEvents;
using ChildCare.Application.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.Email;

public record DailyReportDigestSummary(int SentCount, int SkippedNoEmailCount);

/// <summary>
/// The per-tenant body of the `send-daily-reports` CLI job (feature 020, FR-004/FR-005). The
/// tenant-loop shell itself lives in `ChildCare.Api.Cli.SendDailyReportsCommand`, mirroring
/// `SendPaymentRemindersCommand`/`PaymentReminderNotificationService`'s exact split — the loop
/// resolves `db` per tenant schema (research.md R2), this service does the actual work with an
/// explicit `db` parameter rather than a constructor-injected ambient one.
/// </summary>
public class DailyReportDigestService(
    DailySummaryCalculator calculator,
    IEmailSender emailSender,
    IUnsubscribeTokenService tokenService,
    IConfiguration config,
    ILogger<DailyReportDigestService> logger)
{
    public async Task<DailyReportDigestSummary> ProcessTenantAsync(
        ITenantDbContext db, string organisationSlug, DateOnly date, CancellationToken cancellationToken = default)
    {
        var childIds = await db.ChildGroupAssignments
            .Where(a => a.EndDate == null)
            .Join(db.Children.Where(c => c.DeactivatedAt == null), a => a.ChildId, c => c.Id, (a, c) => c.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        var sentCount = 0;
        var skippedCount = 0;

        foreach (var childId in childIds)
        {
            var child = await db.Children.FirstAsync(c => c.Id == childId, cancellationToken);
            var contacts = await db.ChildContacts
                .Where(cc => cc.ChildId == childId)
                .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => c)
                .ToListAsync(cancellationToken);

            if (contacts.Count == 0)
                continue;

            var summary = await calculator.CalculateAsync(db, childId, date, cancellationToken);

            foreach (var contact in contacts)
            {
                if (contact.Email is null)
                {
                    skippedCount++;
                    logger.LogInformation("Daily report for child {ChildId}: contact {ContactId} has no email on file, skipped.", childId, contact.Id);
                    continue;
                }

                // FR-004/FR-007: the automatic digest — unlike a bulk/announcement/closure email
                // or an on-demand resend — respects the per-contact digest-unsubscribe flag.
                if (contact.DigestUnsubscribedAt is not null)
                    continue;

                var token = tokenService.CreateToken(contact.Id);
                var unsubscribeUrl = EmailLinkBuilder.BuildUnsubscribeUrl(config, token, organisationSlug);

                try
                {
                    await emailSender.SendDailyReportAsync(contact.Email, contact.Locale, child.FirstName, summary, unsubscribeUrl, cancellationToken);
                    sentCount++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Daily report dispatch failed for child {ChildId}, contact {ContactId}.", childId, contact.Id);
                }
            }
        }

        return new DailyReportDigestSummary(sentCount, skippedCount);
    }
}
