using System.Security.Claims;
using ChildCare.Application.Email;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>
/// Feature 020. Director routes (bulk send, attachment upload, recipient-count preview) are
/// DirectorOnly. The daily-report resend is reachable by both director-web and the caregiver
/// tablet (DeviceOrStaffOrDirector, mirrors ChildrenEndpoints/GroupsEndpoints' existing
/// caregiver-plus-director routes). The unsubscribe/resubscribe routes are deliberately
/// unauthenticated (spec.md Security Considerations) — see research.md R5 for why they resolve
/// the tenant schema from a separate `org` query/body parameter rather than a JWT claim.
/// </summary>
public static class EmailEndpoints
{
    public static void MapEmailEndpoints(this WebApplication app)
    {
        var directorGroup = app.MapGroup("/api/email")
            .WithTags("Email")
            .RequireAuthorization("DirectorOnly");

        directorGroup.MapPost("/attachments/upload-url", async (BulkEmailAttachmentUploadUrlRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateBulkEmailAttachmentUploadUrlCommand(req.ContentType));
            return result.Succeeded
                ? Results.Ok(new { uploadUrl = result.UploadUrl, objectPath = result.ObjectPath })
                : MapAttachmentFailure(result.Failure!.Value);
        });

        directorGroup.MapGet("/bulk-send/recipient-count", async (Guid locationId, Guid? groupId, IMediator mediator) =>
        {
            var count = await mediator.Send(new GetBulkEmailRecipientCountQuery(locationId, groupId));
            return Results.Ok(new { recipientCount = count });
        });

        directorGroup.MapPost("/bulk-send", async (SendBulkEmailRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = TenantUserIdOf(ctx);
            var result = await mediator.Send(new SendBulkEmailCommand(
                tenantUserId, req.LocationId, req.GroupId, req.Subject, req.Body,
                req.AttachmentObjectPath, req.AttachmentFileName, req.AttachmentContentType));

            return result.Succeeded
                ? Results.Ok(new
                {
                    bulkEmailSendId = result.BulkEmailSendId,
                    sentCount = result.SentCount,
                    skippedNoEmailCount = result.SkippedNoEmailCount,
                    providerFailureCount = result.ProviderFailureCount,
                })
                : MapSendFailure(result.Failure!.Value);
        });
    }

    private static Guid TenantUserIdOf(HttpContext ctx) => Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private static IResult MapAttachmentFailure(BulkEmailAttachmentFailure failure) => failure switch
    {
        BulkEmailAttachmentFailure.InvalidContentType => Results.Json(
            new { errorKey = "errors.email.invalid_content_type" },
            statusCode: StatusCodes.Status422UnprocessableEntity),

        _ => throw new InvalidOperationException($"Unhandled {nameof(BulkEmailAttachmentFailure)}: {failure}"),
    };

    private static IResult MapSendFailure(SendBulkEmailFailure failure) => failure switch
    {
        SendBulkEmailFailure.AttachmentTooLarge => Results.Json(
            new { errorKey = "errors.email.attachment_too_large" },
            statusCode: StatusCodes.Status422UnprocessableEntity),

        _ => throw new InvalidOperationException($"Unhandled {nameof(SendBulkEmailFailure)}: {failure}"),
    };
}
