using ChildCare.Application.Common;
using ChildCare.Application.Invoices;
using ChildCare.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.FiscalAttestations;

/// <summary>
/// Feature 015 — research.md R3. Aggregates a (child, location, tax year)'s Paid invoices into
/// up to 4 contiguous daily-rate periods. Reuses each invoice's already-stored
/// LineItems.DailyRateCents rather than re-joining Contract (research.md R3's "Alternatives
/// considered" — the invoice's own stored rate is the accurate-to-what-was-billed source).
/// AmountCents per period sums Invoice.TotalCents (actual amount paid, including extra charges)
/// — never Days * DailyRateCents, which would silently drop extra charges (spec.md FR-002).
/// </summary>
public class FiscalAttestationAggregator(ITenantDbContext db)
{
    public async Task<FiscalAttestationAggregationResult> AggregateAsync(
        Guid childId, Guid locationId, int taxYear, CancellationToken cancellationToken = default)
    {
        var yearStart = new DateOnly(taxYear, 1, 1);
        var yearEnd = new DateOnly(taxYear, 12, 31);

        var invoices = await db.Invoices
            .Where(i => i.ChildId == childId && i.LocationId == locationId
                && i.Status == InvoiceStatus.Paid
                && i.PeriodMonth >= yearStart && i.PeriodMonth <= yearEnd)
            .OrderBy(i => i.PeriodMonth)
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0)
            return new FiscalAttestationAggregationResult([], 0);

        var monthly = invoices
            .Select(i =>
            {
                var lineItems = InvoiceLineItems.FromJson(i.LineItems);
                return new MonthlyBillable(i.PeriodMonth, lineItems.PresentDays + lineItems.UnjustifiedAbsentDays, lineItems.DailyRateCents, i.TotalCents);
            })
            .ToList();

        var periods = ConsolidateOverflow(MergeIntoPeriods(monthly));
        var total = periods.Sum(p => p.AmountCents);

        return new FiscalAttestationAggregationResult(periods, total);
    }

    private static List<FiscalAttestationPeriod> MergeIntoPeriods(List<MonthlyBillable> monthly)
    {
        var periods = new List<FiscalAttestationPeriod>();
        MonthlyBillable runStart = monthly[0];
        MonthlyBillable runEnd = monthly[0];
        var runDays = monthly[0].Days;
        var runAmount = monthly[0].AmountCents;

        for (var i = 1; i < monthly.Count; i++)
        {
            var m = monthly[i];
            if (m.DailyRateCents == runStart.DailyRateCents)
            {
                runEnd = m;
                runDays += m.Days;
                runAmount += m.AmountCents;
            }
            else
            {
                periods.Add(ClosePeriod(runStart, runEnd, runDays, runAmount));
                runStart = m;
                runEnd = m;
                runDays = m.Days;
                runAmount = m.AmountCents;
            }
        }

        periods.Add(ClosePeriod(runStart, runEnd, runDays, runAmount));
        return periods;
    }

    private static FiscalAttestationPeriod ClosePeriod(MonthlyBillable start, MonthlyBillable end, int days, int amountCents) =>
        new(
            new DateOnly(start.PeriodMonth.Year, start.PeriodMonth.Month, 1),
            new DateOnly(end.PeriodMonth.Year, end.PeriodMonth.Month, DateTime.DaysInMonth(end.PeriodMonth.Year, end.PeriodMonth.Month)),
            days,
            amountCents,
            start.DailyRateCents);

    // spec.md FR-004/Edge Cases — more than 4 detected periods consolidates the oldest overflow
    // ones into the earliest retained period; DailyRateCents becomes null since the merged
    // period no longer reflects a single rate.
    private static List<FiscalAttestationPeriod> ConsolidateOverflow(List<FiscalAttestationPeriod> periods)
    {
        if (periods.Count <= 4)
            return periods;

        var overflowCount = periods.Count - 4;
        var toMerge = periods.Take(overflowCount + 1).ToList();
        var merged = new FiscalAttestationPeriod(
            toMerge[0].PeriodStart,
            toMerge[^1].PeriodEnd,
            toMerge.Sum(p => p.Days),
            toMerge.Sum(p => p.AmountCents),
            null);

        var result = new List<FiscalAttestationPeriod> { merged };
        result.AddRange(periods.Skip(overflowCount + 1));
        return result;
    }

    private readonly record struct MonthlyBillable(DateOnly PeriodMonth, int Days, int DailyRateCents, int AmountCents);
}

public record FiscalAttestationAggregationResult(IReadOnlyList<FiscalAttestationPeriod> Periods, int TotalAmountCents);
