using System.Globalization;
using System.Xml.Linq;
using System.Xml.Schema;
using ChildCare.Application.Common;

namespace ChildCare.Infrastructure.Sepa;

/// <summary>
/// ISepaBatchXmlGenerator implementation (feature 026, research.md R1/R2). Builds the
/// pain.008.001.02 Document tree directly via System.Xml.Linq, then validates it against the
/// embedded official EPC/ISO20022 schema (Sepa/Schemas/pain.008.001.02.xsd) before returning it —
/// a validation failure throws SepaBatchGenerationException, never a raw XmlSchemaException
/// (spec.md FR-006, Constitution Principle VI). One PmtInf block per batch; SeqTp (FRST/RCUR) is
/// set per DrctDbtTxInf via its own optional PmtTpInf, which the schema explicitly allows to
/// override the (omitted, here) PmtInf-level default — confirmed against the schema itself
/// (PaymentTypeInformation20 appears at both PaymentInstructionInformation4 and
/// DirectDebitTransactionInformation9), so a single batch can freely mix FRST and RCUR
/// instructions without needing separate PmtInf blocks per sequence type.
/// </summary>
public class SepaBatchXmlGenerator : ISepaBatchXmlGenerator
{
    private const string Namespace = "urn:iso:std:iso:20022:tech:xsd:pain.008.001.02";
    private static readonly XNamespace Ns = Namespace;

    private static readonly Lazy<XmlSchemaSet> SchemaSet = new(LoadSchemaSet);

    public SepaBatchXmlResult Generate(
        SepaBatchCreditor creditor,
        IReadOnlyList<SepaDebitInstruction> instructions,
        DateOnly executionDate,
        string paymentInformationId)
    {
        if (instructions.Count == 0)
            throw new SepaBatchGenerationException("A SEPA batch must contain at least one debit instruction.");

        var totalCents = instructions.Sum(i => i.AmountCents);
        var nbOfTxs = instructions.Count.ToString(CultureInfo.InvariantCulture);
        var ctrlSum = FormatAmount(totalCents);
        var now = DateTime.UtcNow;

        var document = new XElement(Ns + "Document",
            new XElement(Ns + "CstmrDrctDbtInitn",
                new XElement(Ns + "GrpHdr",
                    new XElement(Ns + "MsgId", paymentInformationId),
                    new XElement(Ns + "CreDtTm", now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)),
                    new XElement(Ns + "NbOfTxs", nbOfTxs),
                    new XElement(Ns + "CtrlSum", ctrlSum),
                    new XElement(Ns + "InitgPty",
                        new XElement(Ns + "Nm", Truncate(creditor.CreditorName, 140)))),
                new XElement(Ns + "PmtInf",
                    new XElement(Ns + "PmtInfId", paymentInformationId),
                    new XElement(Ns + "PmtMtd", "DD"),
                    new XElement(Ns + "NbOfTxs", nbOfTxs),
                    new XElement(Ns + "CtrlSum", ctrlSum),
                    new XElement(Ns + "PmtTpInf",
                        new XElement(Ns + "SvcLvl", new XElement(Ns + "Cd", "SEPA")),
                        new XElement(Ns + "LclInstrm", new XElement(Ns + "Cd", "CORE"))),
                    new XElement(Ns + "ReqdColltnDt", executionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    new XElement(Ns + "Cdtr", new XElement(Ns + "Nm", Truncate(creditor.CreditorName, 140))),
                    new XElement(Ns + "CdtrAcct", new XElement(Ns + "Id", new XElement(Ns + "IBAN", Normalize(creditor.CreditorIban)))),
                    NotProvidedAgent(),
                    new XElement(Ns + "ChrgBr", "SLEV"),
                    new XElement(Ns + "CdtrSchmeId",
                        new XElement(Ns + "Id",
                            new XElement(Ns + "PrvtId",
                                new XElement(Ns + "Othr",
                                    new XElement(Ns + "Id", creditor.CreditorIdentifier),
                                    new XElement(Ns + "SchmeNm", new XElement(Ns + "Prtry", "SEPA")))))),
                    instructions.Select(BuildDebitTransaction))));

        var xDocument = new XDocument(new XDeclaration("1.0", "UTF-8", null), document);
        Validate(xDocument);

        using var stream = new MemoryStream();
        xDocument.Save(stream);
        return new SepaBatchXmlResult(stream.ToArray(), totalCents, instructions.Count);
    }

    // EndToEndId/MndtId are NEVER truncated, unlike the display-only Nm fields below — silently
    // shortening a mandate reference or end-to-end ID risks pointing at the wrong mandate/invoice
    // at the bank, which is worse than failing loudly. In practice neither ever approaches
    // Max35Text's limit (OgmReferenceGenerator/SepaMandateReferenceGenerator both produce short,
    // fixed-format values) — an over-length one here is a defensive/test scenario, and the schema
    // validator (Validate, below) is the intended, correct way to catch it (spec.md FR-006).
    private static XElement BuildDebitTransaction(SepaDebitInstruction instruction) =>
        new(Ns + "DrctDbtTxInf",
            new XElement(Ns + "PmtId", new XElement(Ns + "EndToEndId", instruction.EndToEndId)),
            new XElement(Ns + "PmtTpInf", new XElement(Ns + "SeqTp", instruction.SequenceType)),
            new XElement(Ns + "InstdAmt", new XAttribute("Ccy", "EUR"), FormatAmount(instruction.AmountCents)),
            new XElement(Ns + "DrctDbtTx",
                new XElement(Ns + "MndtRltdInf",
                    new XElement(Ns + "MndtId", instruction.MandateReference),
                    new XElement(Ns + "DtOfSgntr", instruction.MandateSigningDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))),
            NotProvidedAgent(elementName: "DbtrAgt"),
            new XElement(Ns + "Dbtr", new XElement(Ns + "Nm", Truncate(instruction.DebtorName, 140))),
            new XElement(Ns + "DbtrAcct", new XElement(Ns + "Id", new XElement(Ns + "IBAN", Normalize(instruction.DebtorIban)))));

    // BIC is optional under the SEPA IBAN-only rule; "NOTPROVIDED" is the standard placeholder
    // when no BIC is collected (this codebase captures IBAN only, feature 024).
    private static XElement NotProvidedAgent(string elementName = "CdtrAgt") =>
        new(Ns + elementName,
            new XElement(Ns + "FinInstnId",
                new XElement(Ns + "Othr", new XElement(Ns + "Id", "NOTPROVIDED"))));

    private static string FormatAmount(int cents) =>
        (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    private static string Normalize(string iban) => iban.Replace(" ", string.Empty).ToUpperInvariant();

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static void Validate(XDocument xDocument)
    {
        try
        {
            xDocument.Validate(SchemaSet.Value, (_, e) => throw new XmlSchemaValidationException(e.Message, e.Exception));
        }
        catch (XmlSchemaValidationException ex)
        {
            throw new SepaBatchGenerationException("The generated SEPA batch failed pain.008.001.02 schema validation.", ex);
        }
    }

    private static XmlSchemaSet LoadSchemaSet()
    {
        var assembly = typeof(SepaBatchXmlGenerator).Assembly;
        var resourceName = $"{assembly.GetName().Name}.Sepa.Schemas.pain.008.001.02.xsd";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"pain.008.001.02.xsd not found as embedded resource '{resourceName}'.");
        using var reader = System.Xml.XmlReader.Create(stream);

        var schemaSet = new XmlSchemaSet();
        schemaSet.Add(Namespace, reader);
        schemaSet.Compile();
        return schemaSet;
    }
}
