using System.Threading.RateLimiting;

namespace ChildCare.Api.RateLimiting;

/// <summary>
/// Feature 023 convergence (tasks.md T067): the sliding-window options behind each named
/// rate-limit policy in Program.cs, extracted so they have a single source of truth that a unit
/// test can exercise directly. `AddRateLimiter`'s middleware is deliberately disabled in the
/// "Testing" environment (Program.cs, to avoid flaky integration-test failures under rapid-fire
/// test traffic — see AuthSessionLifecycleTests.LoginEndpoint_StillDeclaresAuthStrictRateLimitPolicy's
/// comment for the established precedent), so this is the only way to prove a policy's actual
/// throttling behavior rather than just its structural wiring.
/// </summary>
public static class RateLimiterPolicies
{
    public static SlidingWindowRateLimiterOptions PublicEnrollment => new()
    {
        PermitLimit          = 3,
        Window               = TimeSpan.FromHours(1),
        SegmentsPerWindow    = 4,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit           = 0,
    };
}
