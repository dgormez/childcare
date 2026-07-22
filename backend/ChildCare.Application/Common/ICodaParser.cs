namespace ChildCare.Application.Common;

/// <summary>
/// Port for parsing a Belgian CODA bank-statement file (feature 025, research.md R1). Mirrors
/// IInvoicePdfGenerator wrapping QuestPDF — Application never references the CodaParser NuGet
/// package's types directly.
/// </summary>
public interface ICodaParser
{
    /// <summary>
    /// Parses a CODA file's raw lines. Throws <see cref="CodaParseException"/> for anything not a
    /// well-formed CODA statement (FR-002) — never the underlying library's own exception type.
    /// </summary>
    IReadOnlyList<CodaParsedTransaction> Parse(IEnumerable<string> lines);
}

/// <param name="ValueDate">The transaction's value/valuta date.</param>
/// <param name="AmountCents">Signed — negative for a reversal (FR-016).</param>
/// <param name="SenderIban">The counterparty's account number.</param>
/// <param name="SenderName">The counterparty's name.</param>
/// <param name="Communication">
/// The raw free-text message, or — when the line carries a structured (OGM-shaped) message —
/// its raw digits, matching what Invoice.OgmReference's digits look like once its display
/// punctuation (+++XXX/XXXX/XXXXX+++) is stripped (research.md R1).
/// </param>
/// <param name="IsStructuredCommunication">
/// Whether <paramref name="Communication"/> came from the line's structured-message field
/// (type 101/102) rather than free text — FR-004's exact match only ever applies when true.
/// </param>
public record CodaParsedTransaction(
    DateOnly ValueDate,
    int AmountCents,
    string SenderIban,
    string SenderName,
    string Communication,
    bool IsStructuredCommunication);

/// <summary>
/// A CODA file failed to parse — wraps the underlying library's exception without leaking it
/// (FR-002, Principle VI). The message is a stable, non-implementation-specific summary; the
/// original exception is preserved as InnerException for server-side logging only.
/// </summary>
public class CodaParseException(string message, Exception innerException) : Exception(message, innerException);
