using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ChildCare.Application.Organisations;

/// <summary>research.md R14: derive a URL/identifier-safe slug from an organisation name.</summary>
public static partial class OrganisationSlugGenerator
{
    private const int MaxSlugLength = 40; // leaves room for the "tenant_" prefix within Postgres's 63-char identifier limit

    public static string FromName(string organisationName)
    {
        var normalized = organisationName
            .Normalize(NormalizationForm.FormD);

        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var ascii = NonAlphanumeric().Replace(sb.ToString().ToLowerInvariant(), "-");
        ascii = MultipleDashes().Replace(ascii, "-").Trim('-');

        if (ascii.Length > MaxSlugLength)
            ascii = ascii[..MaxSlugLength].Trim('-');

        return string.IsNullOrEmpty(ascii) ? "organisation" : ascii;
    }

    /// <summary>Appends a short random suffix on a collision — retried once (research.md R14).</summary>
    public static string WithCollisionSuffix(string baseSlug)
    {
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant(); // 6 hex chars
        return $"{baseSlug}-{suffix}";
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultipleDashes();
}
