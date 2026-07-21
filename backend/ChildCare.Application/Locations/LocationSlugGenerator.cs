using ChildCare.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Locations;

/// <summary>
/// Generates the unique-per-tenant `PublicEnrollmentSlug` a new `Location` needs (feature 023,
/// data-model.md) — every location gets one at creation, independent of whether public
/// enrollment is ever enabled, mirroring how the migration backfilled pre-existing rows
/// (non-alphanumeric runs collapse to a hyphen, a numeric suffix disambiguates a collision).
/// </summary>
internal static class LocationSlugGenerator
{
    public static async Task<string> GenerateAsync(ITenantDbContext db, string name, CancellationToken cancellationToken)
    {
        var baseSlug = Slugify(name);
        if (string.IsNullOrEmpty(baseSlug))
            baseSlug = "location";

        var candidate = baseSlug;
        var suffix = 1;
        while (await db.Locations.AnyAsync(l => l.PublicEnrollmentSlug == candidate, cancellationToken))
        {
            suffix++;
            candidate = $"{baseSlug}-{suffix}";
        }

        return candidate;
    }

    private static string Slugify(string name)
    {
        var lowered = name.Trim().ToLowerInvariant();
        var chars = new char[lowered.Length];
        var count = 0;
        var lastWasHyphen = false;

        foreach (var c in lowered)
        {
            if (char.IsLetterOrDigit(c))
            {
                chars[count++] = c;
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen && count > 0)
            {
                chars[count++] = '-';
                lastWasHyphen = true;
            }
        }

        var result = new string(chars, 0, count).TrimEnd('-');
        return result;
    }
}
