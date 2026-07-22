using ChildCare.Application.Common;

namespace ChildCare.Api.Tests;

/// <summary>
/// Test double for ICodaParser — registered Singleton in OrganisationOnboardingWebAppFactory,
/// overriding Program.cs's real CodaParserAdapter. Unlike FakePaymentProvider/FakeExpoPushSender
/// (which avoid real *external network* calls), CodaParserAdapter makes no network call at all —
/// this fake exists instead because hand-authoring the real Belgian CODA fixed-width format
/// (header/old-balance/movement/new-balance/trailer records, precise byte-column offsets) for
/// every test scenario would be its own fragile piece of test infrastructure. The real format's
/// parsing correctness is already proven separately against genuine sample files in
/// CodaParserAdapterTests (tasks.md T008); these API-level tests exist to prove
/// ImportCodaFileCommand's matching/persistence/invoice-update behavior, not to re-prove parsing.
///
/// Test-only sentinel line format (never a real CODA file): one transaction per line,
/// pipe-delimited: "valueDate|amountCents|senderIban|senderName|communication|isStructured".
/// A line that doesn't match this shape makes Parse throw CodaParseException, so FR-002's
/// malformed-file path is still exercisable through the real endpoint.
/// </summary>
public class FakeCodaParser : ICodaParser
{
    public IReadOnlyList<CodaParsedTransaction> Parse(IEnumerable<string> lines)
    {
        var materialized = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (materialized.Count == 0)
            throw new CodaParseException("The uploaded file is not a valid CODA statement.", new InvalidOperationException("No data given"));

        try
        {
            return materialized.Select(ParseLine).ToList();
        }
        catch (Exception ex) when (ex is not CodaParseException)
        {
            throw new CodaParseException("The uploaded file is not a valid CODA statement.", ex);
        }
    }

    private static CodaParsedTransaction ParseLine(string line)
    {
        var parts = line.Split('|');
        if (parts.Length != 6)
            throw new FormatException($"Expected 6 pipe-delimited fields, got {parts.Length}.");

        return new CodaParsedTransaction(
            ValueDate: DateOnly.Parse(parts[0]),
            AmountCents: int.Parse(parts[1]),
            SenderIban: parts[2],
            SenderName: parts[3],
            Communication: parts[4],
            IsStructuredCommunication: bool.Parse(parts[5]));
    }
}
