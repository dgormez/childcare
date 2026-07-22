using ChildCare.Application.SepaBatches;
using Xunit;

namespace ChildCare.Api.Tests.SepaBatches;

/// <summary>
/// Feature 026, tasks.md T011. Pure-logic unit tests for the FRST/RCUR decision (FR-002a,
/// research.md R3) — the caller (GenerateSepaBatchCommandTests, integration-level) is
/// responsible for proving that "already used" is computed from the immutable
/// SepaMandateReferenceUsed snapshot and correctly resets after a revoke-and-resign; this test
/// only covers the resolver's own trivial-but-critical branch logic.
/// </summary>
public class SepaSequenceTypeResolverTests
{
    [Fact]
    public void Resolve_NoPriorUse_ReturnsFrst()
    {
        Assert.Equal("FRST", SepaSequenceTypeResolver.Resolve(mandateReferenceAlreadyUsed: false));
    }

    [Fact]
    public void Resolve_PriorUse_ReturnsRcur()
    {
        Assert.Equal("RCUR", SepaSequenceTypeResolver.Resolve(mandateReferenceAlreadyUsed: true));
    }
}
