using System.Threading.RateLimiting;
using ChildCare.Api.RateLimiting;
using Xunit;

namespace ChildCare.Api.Tests.PlatformAdmin;

/// <summary>Feature 032, FR-011a (research.md R13/R15): the "organisation-register" sliding-window
/// policy's actual throttling behavior (3/IP/rolling hour), exercised directly against the same
/// options Program.cs wires into the policy, since AddRateLimiter's middleware itself is disabled
/// in the Testing environment (mirrors PublicEnrollmentTests' identical pattern for the sibling
/// public-enrollment policy).</summary>
public class OrganisationRegisterRateLimitTests
{
    [Fact]
    public void OrganisationRegisterRateLimitPolicy_AllowsThreePerHour_RejectsFourth()
    {
        using var limiter = new SlidingWindowRateLimiter(RateLimiterPolicies.OrganisationRegister);

        using var first = limiter.AttemptAcquire();
        using var second = limiter.AttemptAcquire();
        using var third = limiter.AttemptAcquire();
        using var fourth = limiter.AttemptAcquire();

        Assert.True(first.IsAcquired);
        Assert.True(second.IsAcquired);
        Assert.True(third.IsAcquired);
        Assert.False(fourth.IsAcquired);
    }
}
