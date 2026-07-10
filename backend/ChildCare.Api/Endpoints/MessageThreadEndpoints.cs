namespace ChildCare.Api.Endpoints;

/// <summary>
/// Parent routes are ParentOnly; director/staff read routes are DirectorOnly (no staff web UI
/// ships in v1); the reply route is StaffOrDirector regardless, matching every other endpoint's
/// authorization pattern in this codebase (feature 013, spec.md Assumptions).
/// </summary>
public static class MessageThreadEndpoints
{
    public static void MapMessageThreadEndpoints(this WebApplication app)
    {
    }
}
