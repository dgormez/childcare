using ChildCare.Application.Common;
using ChildCare.Infrastructure.Coda;
using Xunit;
using Xunit.Abstractions;

namespace ChildCare.Api.Tests.CodaTransactions;

/// <summary>
/// Feature 025, tasks.md T008. CodaParserAdapter wraps the CodaParser NuGet package
/// (research.md R1) — these tests exercise it against real Belgian CODA sample files (from the
/// library's own test suite) rather than hand-crafted lines, since the fixed-width format is not
/// something worth hand-authoring incorrectly.
/// </summary>
public class CodaParserAdapterTests(ITestOutputHelper output)
{
    private static string[] ReadFixture(string name) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "CodaTransactions", "Fixtures", name));

    [Fact]
    public void Parse_WellFormedFile_ReturnsTransactions()
    {
        var lines = ReadFixture("sample1.cod");

        var result = new CodaParserAdapter().Parse(lines);

        Assert.NotEmpty(result);
        foreach (var t in result)
            output.WriteLine($"ValueDate={t.ValueDate} AmountCents={t.AmountCents} SenderIban={t.SenderIban} SenderName={t.SenderName} Communication='{t.Communication}' Structured={t.IsStructuredCommunication}");
    }

    [Fact]
    public void Parse_FileWithStructuredCommunication_DistinguishesFromFreeText()
    {
        var lines = ReadFixture("sample6.cod");

        var result = new CodaParserAdapter().Parse(lines);

        Assert.NotEmpty(result);
        foreach (var t in result)
            output.WriteLine($"ValueDate={t.ValueDate} AmountCents={t.AmountCents} SenderIban={t.SenderIban} SenderName={t.SenderName} Communication='{t.Communication}' Structured={t.IsStructuredCommunication}");

        // At least one transaction in this fixture carries a structured message, at least one
        // carries free text — proves the library's own type bit is surfaced, not just always false.
        Assert.Contains(result, t => t.IsStructuredCommunication);
    }

    [Fact]
    public void Parse_MalformedFile_ThrowsCodaParseException_NotRawLibraryException()
    {
        string[] garbageLines = ["this is not a coda file", "neither is this"];

        var ex = Assert.Throws<CodaParseException>(() => new CodaParserAdapter().Parse(garbageLines));
        Assert.Equal("The uploaded file is not a valid CODA statement.", ex.Message);
    }

    [Fact]
    public void Parse_EmptyFile_ThrowsCodaParseException()
    {
        Assert.Throws<CodaParseException>(() => new CodaParserAdapter().Parse([]));
    }
}
