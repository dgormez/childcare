using ChildCare.Application.Common;

namespace ChildCare.Api.Tests;

/// <summary>
/// Test doubles for the ports introduced by research.md R7 — registered Singleton in
/// OrganisationOnboardingWebAppFactory, overriding Program.cs's real (Scoped) validators, so
/// tests can deterministically control "this token is valid and belongs to this identity"
/// without a real Google/Apple round-trip (mirrors TenantMiddleware.FailureInjectionHookForTests'
/// settable-property-on-a-singleton pattern). Behavior must be cleared after each test that sets it.
/// </summary>
public class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    public Func<string, GoogleIdentity?>? Behavior { get; set; }

    public Task<GoogleIdentity?> ValidateAsync(string idToken) =>
        Task.FromResult(Behavior?.Invoke(idToken));
}

public class FakeAppleTokenValidator : IAppleTokenValidator
{
    public Func<string, string, AppleIdentity?>? Behavior { get; set; }

    public Task<AppleIdentity?> ValidateAsync(string identityToken, string bundleId) =>
        Task.FromResult(Behavior?.Invoke(identityToken, bundleId));
}
