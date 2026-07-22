namespace ChildCare.Application.Common;

/// <summary>
/// Port for generating a SEPA pain.008.001.02 batch (feature 026, research.md R1/R2). Builds the
/// XML tree directly (System.Xml.Linq, no third-party SEPA-generation package) and validates it
/// against the official, embedded EPC/ISO20022 schema before returning it — mirrors
/// IInvoicePdfGenerator/IContractPdfGenerator's port/adapter split, so Application never
/// references System.Xml.Schema directly. Also computes the message-level control totals
/// (NbOfTxs/CtrlSum, 026 spec.md FR-006) from the actual instruction list — never a
/// caller-supplied value that could drift from it — and returns them alongside the XML bytes so
/// the caller can persist the exact same total shown to the director (FR-008).
/// </summary>
public interface ISepaBatchXmlGenerator
{
    SepaBatchXmlResult Generate(
        SepaBatchCreditor creditor,
        IReadOnlyList<SepaDebitInstruction> instructions,
        DateOnly executionDate,
        string paymentInformationId);
}

public record SepaBatchCreditor(string CreditorIdentifier, string CreditorName, string CreditorIban);

public record SepaDebitInstruction(
    string EndToEndId,
    int AmountCents,
    string DebtorIban,
    string DebtorName,
    string MandateReference,
    DateOnly MandateSigningDate,
    string SequenceType);

public record SepaBatchXmlResult(byte[] Xml, int TotalCents, int InvoiceCount);

/// <summary>
/// Thrown when the generated XML fails validation against the embedded pain.008.001.02 schema —
/// never let the raw XmlSchemaException/XmlSchemaValidationException surface to the client
/// (026 spec.md FR-006, Principle VI).
/// </summary>
public class SepaBatchGenerationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
