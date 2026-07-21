using System.Numerics;
using System.Text.RegularExpressions;

namespace ChildCare.Application.Common;

/// <summary>
/// IBAN format + mod-97 checksum validation (feature 024-esignature, FR-008). Validates any
/// SEPA-scope IBAN, not Belgium-only (spec.md Edge Cases: "A parent's IBAN belongs to a
/// non-Belgian SEPA country — accepted... only the IBAN's own checksum/format is validated, not
/// its country").
/// </summary>
public static partial class IbanValidation
{
    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
    private static partial Regex IbanShapeRegex();

    public static bool IsValid(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
            return false;

        var normalized = iban.Replace(" ", string.Empty).ToUpperInvariant();
        if (!IbanShapeRegex().IsMatch(normalized))
            return false;

        // Move the first 4 characters to the end, then convert letters to numbers (A=10, ...,
        // Z=35) before computing mod 97 — the standard ISO 7064 MOD97-10 IBAN check.
        var rearranged = normalized[4..] + normalized[..4];
        var numeric = new System.Text.StringBuilder(rearranged.Length * 2);
        foreach (var c in rearranged)
            numeric.Append(char.IsLetter(c) ? (c - 'A' + 10).ToString() : c.ToString());

        return BigInteger.Parse(numeric.ToString()) % 97 == 1;
    }
}
