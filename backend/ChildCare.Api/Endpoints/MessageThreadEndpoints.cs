using System.Security.Claims;
using ChildCare.Application.Messaging;
using ChildCare.Contracts.Requests;
using MediatR;

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
        var parentGroup = app.MapGroup("/api/parent/message-threads")
            .WithTags("Messaging")
            .RequireAuthorization("ParentOnly");

        parentGroup.MapPost("/", async (CreateMessageThreadRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var result = await mediator.Send(new CreateMessageThreadCommand(tenantUserId, req.ChildId, req.Subject, req.Body));
            return MapResult(result, onSuccess: r => Results.Created($"/api/parent/message-threads/{r.Id}", r));
        });

        parentGroup.MapGet("/", async (HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var threads = await mediator.Send(new ListParentThreadsQuery(tenantUserId));
            return Results.Ok(threads);
        });

        parentGroup.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var result = await mediator.Send(new GetThreadQuery(tenantUserId, id, IsStaffOrDirector: false));
            return MapResult(result, onSuccess: Results.Ok);
        });

        parentGroup.MapPost("/{id:guid}/messages", async (Guid id, SendMessageRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var result = await mediator.Send(new SendMessageCommand(tenantUserId, id, req.Body, IsStaffOrDirector: false));
            return MapSendResult(result, onSuccess: r => Results.Created($"/api/parent/message-threads/{id}/messages/{r.Id}", r));
        });

        var orgGroup = app.MapGroup("/api/message-threads")
            .WithTags("Messaging")
            .RequireAuthorization("DirectorOnly");

        orgGroup.MapGet("/", async (IMediator mediator) =>
        {
            var threads = await mediator.Send(new ListOrgThreadsQuery());
            return Results.Ok(threads);
        });

        orgGroup.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var result = await mediator.Send(new GetThreadQuery(tenantUserId, id, IsStaffOrDirector: true));
            return MapResult(result, onSuccess: Results.Ok);
        });

        // Standalone route rather than inside the DirectorOnly group — a more permissive
        // per-route policy cannot live inside a stricter group-level one (research.md R6,
        // specs/008-caregiver-app-scaffold precedent).
        app.MapPost("/api/message-threads/{id:guid}/messages", async (Guid id, SendMessageRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var result = await mediator.Send(new SendMessageCommand(tenantUserId, id, req.Body, IsStaffOrDirector: true));
            return MapSendResult(result, onSuccess: r => Results.Created($"/api/message-threads/{id}/messages/{r.Id}", r));
        }).WithTags("Messaging").RequireAuthorization("StaffOrDirector");
    }

    private static Guid TenantUserIdOf(HttpContext ctx) => Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private static IResult MapResult(MessageThreadResult result, Func<Contracts.Responses.MessageThreadResponse, IResult> onSuccess)
        => result.Succeeded ? onSuccess(result.Response!) : MapFailure(result.Failure!.Value);

    private static IResult MapSendResult(SendMessageResult result, Func<Contracts.Responses.MessageResponse, IResult> onSuccess)
        => result.Succeeded ? onSuccess(result.Response!) : MapFailure(result.Failure!.Value);

    private static IResult MapFailure(MessagingFailure failure) => failure switch
    {
        MessagingFailure.ChildNotFound => Results.Json(
            new { errorKey = "errors.children.not_found" },
            statusCode: StatusCodes.Status404NotFound),

        MessagingFailure.ThreadNotFound => Results.Json(
            new { errorKey = "errors.message_thread.not_found" },
            statusCode: StatusCodes.Status404NotFound),

        MessagingFailure.NotParticipant => Results.Json(
            new { errorKey = "errors.message_thread.not_participant" },
            statusCode: StatusCodes.Status403Forbidden),

        _ => throw new InvalidOperationException($"Unhandled {nameof(MessagingFailure)}: {failure}"),
    };
}
