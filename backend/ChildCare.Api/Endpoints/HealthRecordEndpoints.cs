using System.Security.Claims;
using ChildCare.Application.HealthRecords;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 013c — contracts/vaccine-health-records-api.md.</summary>
public static class HealthRecordEndpoints
{
    private const int UploadUrlExpiresInSeconds = 900;

    public static void MapHealthRecordEndpoints(this WebApplication app)
    {
        // 031-photo-lifecycle-governance FR-011: staff previously had zero access to health
        // records of any kind (create, edit, attachment-upload, and delete were all
        // DirectorOnly, not a delete-lags-upload asymmetry) — widened to StaffOrDirector so
        // staff and director are treated identically here, matching group-activity photos.
        var group = app.MapGroup("/api/children/{childId:guid}/health-records")
            .WithTags("HealthRecords")
            .RequireAuthorization("StaffOrDirector");

        group.MapPost("/", async (Guid childId, CreateHealthRecordRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (role, tenantUserId) = ChildrenEndpoints.CallerIdentity(ctx);
            var result = await mediator.Send(new CreateHealthRecordCommand(
                childId, req.RecordType, req.Title, req.Description, req.ValidFrom, req.ValidUntil, TenantUserIdOf(ctx), role, tenantUserId));
            return MapResult(result, onSuccess: r => Results.Created($"/api/children/{childId}/health-records/{r.Id}", r));
        });

        group.MapGet("/", async (Guid childId, IMediator mediator) =>
        {
            var list = await mediator.Send(new ListChildHealthRecordsQuery(childId));
            return Results.Ok(list);
        });

        group.MapPut("/{id:guid}", async (Guid childId, Guid id, UpdateHealthRecordRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (role, tenantUserId) = ChildrenEndpoints.CallerIdentity(ctx);
            var result = await mediator.Send(new UpdateHealthRecordCommand(childId, id, req.RecordType, req.Title, req.Description, req.ValidFrom, req.ValidUntil, role, tenantUserId));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapDelete("/{id:guid}", async (Guid childId, Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var (role, tenantUserId) = ChildrenEndpoints.CallerIdentity(ctx);
            var result = await mediator.Send(new DeleteHealthRecordCommand(childId, id, role, tenantUserId));
            return result.Succeeded ? Results.NoContent() : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/{id:guid}/attachment-upload-url", async (Guid childId, Guid id, CreateHealthRecordAttachmentUploadUrlRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (role, tenantUserId) = ChildrenEndpoints.CallerIdentity(ctx);
            var result = await mediator.Send(new CreateHealthRecordAttachmentUploadUrlCommand(childId, id, req.ContentType, role, tenantUserId));
            return result.Succeeded
                ? Results.Ok(new CreateHealthRecordAttachmentUploadUrlResponse(result.UploadUrl!, UploadUrlExpiresInSeconds))
                : MapFailure(result.Failure!.Value);
        });
    }

    private static Guid TenantUserIdOf(HttpContext ctx) => Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private static IResult MapResult(HealthRecordResult result, Func<HealthRecordResponse, IResult> onSuccess) =>
        result.Succeeded ? onSuccess(result.Response!) : MapFailure(result.Failure!.Value);

    private static IResult MapFailure(HealthRecordFailure failure) => failure switch
    {
        HealthRecordFailure.ChildNotFound => Results.Json(
            new { errorKey = "errors.children.not_found" }, statusCode: StatusCodes.Status404NotFound),

        HealthRecordFailure.NotFound => Results.Json(
            new { errorKey = "errors.health_records.not_found" }, statusCode: StatusCodes.Status404NotFound),

        HealthRecordFailure.InvalidContentType => Results.Json(
            new { errorKey = "errors.health_records.attachment_content_type_invalid" }, statusCode: StatusCodes.Status422UnprocessableEntity),

        _ => throw new InvalidOperationException($"Unhandled {nameof(HealthRecordFailure)}: {failure}"),
    };
}
