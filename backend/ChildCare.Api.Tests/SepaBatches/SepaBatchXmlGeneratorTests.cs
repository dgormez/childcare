using ChildCare.Application.Common;
using ChildCare.Infrastructure.Sepa;
using Xunit;

namespace ChildCare.Api.Tests.SepaBatches;

/// <summary>
/// Feature 026, tasks.md T009. Exercises SepaBatchXmlGenerator against the real embedded
/// pain.008.001.02 schema (research.md R2) — a valid input must produce schema-valid XML with
/// the expected element values; an input that would produce schema-invalid XML must throw
/// SepaBatchGenerationException rather than return an invalid document (spec.md FR-006).
/// </summary>
public class SepaBatchXmlGeneratorTests
{
    private static readonly SepaBatchCreditor Creditor = new(
        CreditorIdentifier: "BE68ZZZ0123456789",
        CreditorName: "KDV De Zonnebloem",
        CreditorIban: "BE68539007547034");

    [Fact]
    public void Generate_ValidInput_ProducesSchemaValidXmlWithExpectedValues()
    {
        var instructions = new[]
        {
            new SepaDebitInstruction(
                EndToEndId: "+++123/4567/89012+++",
                AmountCents: 45000,
                DebtorIban: "BE71096123456769",
                DebtorName: "Jan Janssens",
                MandateReference: "MND-0001",
                MandateSigningDate: new DateOnly(2026, 1, 15),
                SequenceType: "FRST"),
            new SepaDebitInstruction(
                EndToEndId: "+++987/6543/21098+++",
                AmountCents: 32500,
                DebtorIban: "BE62510007547061",
                DebtorName: "Marie Dubois",
                MandateReference: "MND-0002",
                MandateSigningDate: new DateOnly(2025, 11, 3),
                SequenceType: "RCUR"),
        };

        var generator = new SepaBatchXmlGenerator();
        var result = generator.Generate(Creditor, instructions, new DateOnly(2026, 8, 5), "BATCH-0001");

        Assert.Equal(77500, result.TotalCents);
        Assert.Equal(2, result.InvoiceCount);

        var xml = System.Text.Encoding.UTF8.GetString(result.Xml);
        Assert.Contains("<NbOfTxs>2</NbOfTxs>", xml);
        Assert.Contains("<CtrlSum>775.00</CtrlSum>", xml);
        Assert.Contains("<SeqTp>FRST</SeqTp>", xml);
        Assert.Contains("<SeqTp>RCUR</SeqTp>", xml);
        Assert.Contains("<IBAN>BE71096123456769</IBAN>", xml);
        Assert.Contains("<MndtId>MND-0001</MndtId>", xml);
        Assert.Contains("<DtOfSgntr>2026-01-15</DtOfSgntr>", xml);
        Assert.Contains("<EndToEndId>+++123/4567/89012+++</EndToEndId>", xml);
        Assert.Contains("<ReqdColltnDt>2026-08-05</ReqdColltnDt>", xml);
        Assert.Contains("<Id>BE68ZZZ0123456789</Id>", xml);
    }

    [Fact]
    public void Generate_NoInstructions_Throws()
    {
        var generator = new SepaBatchXmlGenerator();
        Assert.Throws<SepaBatchGenerationException>(() =>
            generator.Generate(Creditor, [], new DateOnly(2026, 8, 5), "BATCH-0002"));
    }

    [Fact]
    public void Generate_MandateReferenceExceedingSchemaMaxLength_ThrowsInsteadOfReturningInvalidDocument()
    {
        // MndtId is Max35Text — an over-length value must fail schema validation and throw,
        // never silently truncate into a document that no longer names the real mandate at the
        // bank (spec.md FR-006).
        var generator = new SepaBatchXmlGenerator();
        var overLongInstruction = new SepaDebitInstruction(
            EndToEndId: "+++123/4567/89012+++",
            AmountCents: 45000,
            DebtorIban: "BE71096123456769",
            DebtorName: "Jan Janssens",
            MandateReference: new string('A', 36),
            MandateSigningDate: new DateOnly(2026, 1, 15),
            SequenceType: "FRST");

        Assert.Throws<SepaBatchGenerationException>(() =>
            generator.Generate(Creditor, [overLongInstruction], new DateOnly(2026, 8, 5), "BATCH-0003"));
    }
}
