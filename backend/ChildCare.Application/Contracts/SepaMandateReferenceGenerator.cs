using System.Security.Cryptography;

namespace ChildCare.Application.Contracts;

/// <summary>
/// Generates a unique SEPA mandate reference per signing (feature 024-esignature, FR-017,
/// research.md's data-model.md). Same unambiguous alphabet/cryptographic-randomness approach
/// as feature 023's ReferenceCodeGenerator — a mandate reference has no phone-legibility
/// requirement, but reusing the proven generator shape avoids a second, subtly different one.
/// </summary>
internal static class SepaMandateReferenceGenerator
{
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int Length = 8;

    public static string Generate()
    {
        Span<char> chars = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];

        return $"MND-{new string(chars)}";
    }
}
