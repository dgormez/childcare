namespace ChildCare.Application.Children;

// https://nl.wikipedia.org/wiki/Rijksregisternummer — the check (last 2 of the 11 digits) is
// 97 minus the first 9 digits mod 97; people born in 2000 or later compute it against the first
// 9 digits with "2" prefixed (i.e. + 2_000_000_000) instead, since the 2-digit year alone can't
// tell the two centuries apart. A number is valid if either variant's check matches.
public static class RijksregisternummerChecksum
{
    public static bool IsValid(string elevenDigits)
    {
        if (elevenDigits.Length != 11 || !elevenDigits.All(char.IsDigit))
            return false;

        var first9 = long.Parse(elevenDigits[..9]);
        var check = int.Parse(elevenDigits[9..]);

        var beforeYear2000 = 97 - (int)(first9 % 97);
        var fromYear2000 = 97 - (int)((2_000_000_000 + first9) % 97);

        return check == beforeYear2000 || check == fromYear2000;
    }
}
