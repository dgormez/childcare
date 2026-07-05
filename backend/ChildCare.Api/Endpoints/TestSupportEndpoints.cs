namespace ChildCare.Api.Endpoints;

/// <summary>
/// Test-only endpoints proving the three named authorization policies (feature 003, research.md
/// R5) work correctly — mapped only under the "Testing" environment (see Program.cs), never in
/// production. No business logic here; these exist purely for AuthRolePolicyTests.cs to call.
/// </summary>
public static class TestSupportEndpoints
{
    public static void MapTestSupportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/test-support").WithTags("TestSupport");

        group.MapGet("/director-only", () => Results.Ok()).RequireAuthorization("DirectorOnly");
        group.MapGet("/staff-or-director", () => Results.Ok()).RequireAuthorization("StaffOrDirector");
        group.MapGet("/parent-only", () => Results.Ok()).RequireAuthorization("ParentOnly");
    }
}
