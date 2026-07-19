using ChildCare.Application.ChildEvents;
using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Email;

public record ResendDailyReportEmailCommand(Guid ChildId) : IRequest<ResendDailyReportEmailResult>;

public enum ResendDailyReportEmailFailure { ChildNotFound }

public class ResendDailyReportEmailResult
{
    public bool Succeeded { get; private init; }
    public int SentCount { get; private init; }
    public ResendDailyReportEmailFailure? Failure { get; private init; }

    public static ResendDailyReportEmailResult Ok(int sentCount) => new() { Succeeded = true, SentCount = sentCount };
    public static ResendDailyReportEmailResult Fail(ResendDailyReportEmailFailure failure) => new() { Succeeded = false, Failure = failure };
}

/// <summary>
/// User Story 3 (spec.md FR-009): an on-demand resend of one child's daily report, triggered by
/// a director or caregiver — unaffected by digest-unsubscribe state, unlike the automatic digest
/// (DailyReportDigestService). Reuses GetDailySummaryQuery's calculation and the same
/// IEmailSender.SendDailyReportAsync template as the digest, so the resent email is identical in
/// shape to what an automatic digest would have sent for today.
/// </summary>
public class ResendDailyReportEmailCommandHandler(
    ITenantDbContext db,
    DailySummaryCalculator calculator,
    IEmailSender emailSender,
    IUnsubscribeTokenService tokenService,
    ICurrentTenantService currentTenant,
    Microsoft.Extensions.Configuration.IConfiguration config)
    : IRequestHandler<ResendDailyReportEmailCommand, ResendDailyReportEmailResult>
{
    public async Task<ResendDailyReportEmailResult> Handle(ResendDailyReportEmailCommand request, CancellationToken cancellationToken)
    {
        var child = await db.Children.SingleOrDefaultAsync(c => c.Id == request.ChildId, cancellationToken);
        if (child is null)
            return ResendDailyReportEmailResult.Fail(ResendDailyReportEmailFailure.ChildNotFound);

        var contacts = await db.ChildContacts
            .Where(cc => cc.ChildId == request.ChildId)
            .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => c)
            .ToListAsync(cancellationToken);

        var today = BelgianCalendarDay.Today();
        var summary = await calculator.CalculateAsync(db, request.ChildId, today, cancellationToken);

        var sentCount = 0;
        foreach (var contact in contacts)
        {
            if (contact.Email is null)
                continue;

            var token = tokenService.CreateToken(contact.Id);
            var unsubscribeUrl = EmailLinkBuilder.BuildUnsubscribeUrl(config, token, currentTenant.TenantSlug);

            await emailSender.SendDailyReportAsync(contact.Email, contact.Locale, child.FirstName, summary, unsubscribeUrl, cancellationToken);
            sentCount++;
        }

        return ResendDailyReportEmailResult.Ok(sentCount);
    }
}
