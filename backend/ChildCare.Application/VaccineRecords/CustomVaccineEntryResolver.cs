using System.Data.Common;
using System.Globalization;
using System.Text;
using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.VaccineRecords;

/// <summary>
/// Resolves a typed, non-catalog vaccine name to a tenant-scoped remembered entry (spec.md
/// FR-006/FR-007, research.md R3) — creating one on first use, reusing it on every subsequent
/// case/whitespace/diacritic-insensitive match. A DB-level unique index on NormalizedName
/// (data-model.md) is the actual dedupe guarantee; the insert-then-fallback-to-select here
/// handles the race where two directors submit a near-duplicate at the same moment.
/// </summary>
internal static class CustomVaccineEntryResolver
{
    public static async Task<Guid> ResolveAsync(ITenantDbContext db, string vaccineName, CancellationToken cancellationToken)
    {
        var normalizedName = Normalize(vaccineName);

        var existing = await db.TenantCustomVaccineEntries
            .FirstOrDefaultAsync(e => e.NormalizedName == normalizedName, cancellationToken);
        if (existing is not null)
            return existing.Id;

        var entry = new TenantCustomVaccineEntry { Name = vaccineName.Trim(), NormalizedName = normalizedName };
        db.TenantCustomVaccineEntries.Add(entry);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return entry.Id;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Lost the race to a concurrent submission — un-add (Remove on an Added entity
            // detaches it, EF Core's standard idiom for this) and use the winner's row instead.
            db.TenantCustomVaccineEntries.Remove(entry);
            var winner = await db.TenantCustomVaccineEntries
                .FirstAsync(e => e.NormalizedName == normalizedName, cancellationToken);
            return winner.Id;
        }
    }

    /// <summary>Case-fold, trim, and strip diacritics (spec.md FR-007) via Unicode NFKD
    /// normalization + combining-mark removal — done in C#, not a Postgres `unaccent`
    /// dependency (research.md R3).</summary>
    private static string Normalize(string name)
    {
        var trimmedLower = name.Trim().ToLowerInvariant();
        var decomposed = trimmedLower.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is DbException { SqlState: "23505" };
}
