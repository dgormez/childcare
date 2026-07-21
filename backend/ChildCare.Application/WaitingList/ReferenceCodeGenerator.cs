using System.Security.Cryptography;

namespace ChildCare.Application.WaitingList;

/// <summary>
/// Generates the 8-character, human-legible reference code self-registered entries get
/// (spec.md FR-008, Clarifications). Cryptographically random (research.md R5) — not a
/// predictable sequence, since a guessable code would let one submitter enumerate another
/// family's confirmation. Excludes visually-confusable characters (0/O, 1/I/l) so it reads
/// cleanly over the phone.
/// </summary>
internal static class ReferenceCodeGenerator
{
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int Length = 8;

    public static string Generate()
    {
        Span<char> chars = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];

        return new string(chars);
    }
}
