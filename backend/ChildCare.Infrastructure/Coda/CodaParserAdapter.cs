using ChildCare.Application.Common;

namespace ChildCare.Infrastructure.Coda;

/// <summary>
/// ICodaParser implementation wrapping the CodaParser NuGet package (research.md R1). The
/// library throws a bare System.Exception for anything malformed — there is no more specific
/// type to catch, so this adapter narrows the catch to exactly the library calls, never a wider
/// scope, and always rethrows as CodaParseException (FR-002, Principle VI: never let a raw
/// parser exception surface to the client).
/// </summary>
public class CodaParserAdapter : ICodaParser
{
    public IReadOnlyList<CodaParsedTransaction> Parse(IEnumerable<string> lines)
    {
        IEnumerable<global::CodaParser.Statements.Statement> statements;
        try
        {
            statements = new global::CodaParser.Parser().Parse(lines);
        }
        catch (Exception ex)
        {
            throw new CodaParseException("The uploaded file is not a valid CODA statement.", ex);
        }

        return statements
            .SelectMany(statement => statement.Transactions)
            .Select(t => new CodaParsedTransaction(
                ValueDate: DateOnly.FromDateTime(t.ValutaDate),
                AmountCents: (int)Math.Round(t.Amount * 100m, MidpointRounding.AwayFromZero),
                SenderIban: t.Account.Number,
                SenderName: t.Account.Name,
                Communication: t.StructuredMessage ?? t.Message,
                IsStructuredCommunication: t.StructuredMessage is not null))
            .ToList();
    }
}
