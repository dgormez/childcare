using ChildCare.Application.Invoices;
using Xunit;

namespace ChildCare.Api.Tests.Invoices;

/// <summary>
/// Feature 014 — spec.md FR-004. Pure algorithm, no database needed: base number formatting
/// and the modulo-97 checksum, including the remainder-zero-becomes-97 special case.
/// </summary>
public class OgmReferenceGeneratorTests
{
    [Theory]
    [InlineData(1, "+++000/0000/00101+++")]
    [InlineData(123456789, "+++012/3456/78939+++")]
    public void Generate_ProducesCorrectFormatAndChecksum(long sequenceNumber, string expected)
    {
        Assert.Equal(expected, OgmReferenceGenerator.Generate(sequenceNumber));
    }

    [Fact]
    public void Generate_WhenRemainderIsZero_UsesNinetySevenNotZero()
    {
        // 9700000000 % 97 == 0 — the check digits must be "97", never "00".
        var reference = OgmReferenceGenerator.Generate(9_700_000_000);

        Assert.EndsWith("97+++", reference);
        Assert.DoesNotContain("00+++", reference);
    }

    [Fact]
    public void Generate_DifferentSequenceNumbers_ProduceDifferentReferences()
    {
        var first = OgmReferenceGenerator.Generate(1);
        var second = OgmReferenceGenerator.Generate(2);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        Assert.Equal(OgmReferenceGenerator.Generate(42), OgmReferenceGenerator.Generate(42));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10_000_000_000)]
    public void Generate_OutOfRangeSequenceNumber_Throws(long sequenceNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OgmReferenceGenerator.Generate(sequenceNumber));
    }
}
