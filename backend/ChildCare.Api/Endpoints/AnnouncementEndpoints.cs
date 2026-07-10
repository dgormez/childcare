using System.Security.Claims;
using ChildCare.Application.Announcements;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Director routes are DirectorOnly; the parent read route is ParentOnly (feature 013).</summary>
public static class AnnouncementEndpoints
{
    public static void MapAnnouncementEndpoints(this WebApplication app)
    {
        var directorGroup = app.MapGroup("/api/announcements")
            .WithTags("Announcements")
            .RequireAuthorization("DirectorOnly");

        directorGroup.MapPost("/", async (SendAnnouncementRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var result = await mediator.Send(new SendAnnouncementCommand(tenantUserId, req.LocationId, req.GroupId, req.Subject, req.Body));
            return MapResult(result, onSuccess: r => Results.Created($"/api/announcements/{r.Id}", r));
        });

        directorGroup.MapGet("/", async (IMediator mediator) =>
        {
            var announcements = await mediator.Send(new ListAnnouncementsQuery());
            return Results.Ok(announcements);
        });

        app.MapGet("/api/parent/announcements/{id:guid}", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var result = await mediator.Send(new GetParentAnnouncementQuery(tenantUserId, id));
            return result.Succeeded
                ? Results.Ok(result.Response)
                : MapFailure(result.Failure!.Value);
        }).WithTags("Announcements").RequireAuthorization("ParentOnly");
    }

    private static Guid TenantUserIdOf(HttpContext ctx) => Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private static IResult MapResult(AnnouncementResult result, Func<Contracts.Responses.AnnouncementResponse, IResult> onSuccess)
        => result.Succeeded ? onSuccess(result.Response!) : MapFailure(result.Failure!.Value);

    private static IResult MapFailure(AnnouncementFailure failure) => failure switch
    {
        AnnouncementFailure.NotFound => Results.Json(
            new { errorKey = "errors.announcement.not_found" },
            statusCode: StatusCodes.Status404NotFound),

        AnnouncementFailure.NotRecipient => Results.Json(
            new { errorKey = "errors.announcement.not_recipient" },
            statusCode: StatusCodes.Status403Forbidden),

        _ => throw new InvalidOperationException($"Unhandled {nameof(AnnouncementFailure)}: {failure}"),
    };
}
