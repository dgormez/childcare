namespace ChildCare.Application.Invoices;

/// <summary>
/// Feature 014 — spec.md FR-004. Belgian OGM structured payment reference:
/// +++XXX/XXXX/XXXXX+++, 12 digits. The first 10 digits are a base number unique to the
/// invoice (Invoice.SequenceNumber, research.md R3); the final 2 digits are that number modulo
/// 97, except a remainder of 0 becomes 97 (a base number's checksum is never "00").
/// </summary>
public static class OgmReferenceGenerator
{
    public static string Generate(long sequenceNumber)
    {
        if (sequenceNumber < 1 || sequenceNumber > 9_999_999_999)
            throw new ArgumentOutOfRangeException(nameof(sequenceNumber), "OGM base number must fit in 10 digits.");

        var basePart = sequenceNumber.ToString().PadLeft(10, '0');
        var remainder = sequenceNumber % 97;
        var check = remainder == 0 ? 97 : remainder;

        return $"+++{basePart[..3]}/{basePart[3..7]}/{basePart[7..10]}{check:D2}+++";
    }
}
