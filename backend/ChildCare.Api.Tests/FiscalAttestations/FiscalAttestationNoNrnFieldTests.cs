using System.Reflection;
using ChildCare.Application.Common;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using Xunit;

namespace ChildCare.Api.Tests.FiscalAttestations;

/// <summary>Feature 015 — spec.md FR-007/FR-015: the NRN/SSIN MUST NEVER be persisted or
/// transmitted anywhere. Asserted structurally (no field of that shape exists at all) rather
/// than behaviorally, since a behavioral test could only prove "this one code path doesn't set
/// it," not "no code path can."</summary>
public class FiscalAttestationNoNrnFieldTests
{
    private static readonly string[] NrnLikeNames = ["nrn", "ssin", "nationalregistrynumber", "rijksregisternummer", "numeroderegistrenational"];

    [Theory]
    [InlineData(typeof(FiscalAttestation))]
    [InlineData(typeof(FiscalAttestationPdfModel))]
    [InlineData(typeof(FiscalAttestationResponse))]
    [InlineData(typeof(FiscalAttestationPeriodResponse))]
    public void Type_HasNoNrnShapedMember(Type type)
    {
        var memberNames = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name.ToLowerInvariant());

        foreach (var name in memberNames)
            Assert.DoesNotContain(NrnLikeNames, nrn => name.Contains(nrn));
    }
}
