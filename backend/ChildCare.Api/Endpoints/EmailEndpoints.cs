using System.Net;
using System.Security.Claims;
using ChildCare.Api.Services;
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

        var publicGroup = app.MapGroup("/api/email")
            .WithTags("Email")
            .AllowAnonymous();

        publicGroup.MapGet("/unsubscribe", async (string token, string org, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetDigestSubscriptionStateQuery(org, token));
            return Results.Content(RenderUnsubscribePage(result, token, org), "text/html");
        });

        publicGroup.MapPost("/unsubscribe", async (HttpContext ctx, IMediator mediator) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var token = form["token"].ToString();
            var org = form["org"].ToString();
            await mediator.Send(new UnsubscribeDigestCommand(org, token));
            return Results.Redirect($"/api/email/unsubscribe?token={Uri.EscapeDataString(token)}&org={Uri.EscapeDataString(org)}");
        });

        publicGroup.MapPost("/resubscribe", async (HttpContext ctx, IMediator mediator) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var token = form["token"].ToString();
            var org = form["org"].ToString();
            await mediator.Send(new ResubscribeDigestCommand(org, token));
            return Results.Redirect($"/api/email/unsubscribe?token={Uri.EscapeDataString(token)}&org={Uri.EscapeDataString(org)}");
        });
    }

    private static string RenderUnsubscribePage(DigestSubscriptionResult result, string token, string organisationSlug)
    {
        var encodedToken = WebUtility.HtmlEncode(token);
        var encodedOrg = WebUtility.HtmlEncode(organisationSlug);

        if (!result.Valid)
        {
            var invalidLabels = UnsubscribePageLabelsProvider.For("nl");
            return $"""
                <!DOCTYPE html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"></head>
                <body style="font-family:sans-serif;max-width:420px;margin:60px auto;padding:0 16px;color:#1F2937">
                <p>{WebUtility.HtmlEncode(invalidLabels.InvalidLinkText)}</p>
                </body></html>
                """;
        }

        var labels = UnsubscribePageLabelsProvider.For(result.Locale);
        var (statusText, action, buttonText) = result.Unsubscribed
            ? (labels.UnsubscribedText, "resubscribe", labels.ResubscribeButton)
            : (labels.SubscribedText, "unsubscribe", labels.UnsubscribeButton);

        return $"""
            <!DOCTYPE html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"></head>
            <body style="font-family:sans-serif;max-width:420px;margin:60px auto;padding:0 16px;color:#1F2937">
            <h1 style="font-size:18px">{WebUtility.HtmlEncode(labels.Title)}</h1>
            <p>{WebUtility.HtmlEncode(statusText)}</p>
            <form method="post" action="/api/email/{action}">
              <input type="hidden" name="token" value="{encodedToken}" />
              <input type="hidden" name="org" value="{encodedOrg}" />
              <button type="submit" style="background:#4F7CAC;color:#fff;padding:10px 20px;border:none;border-radius:8px;font-size:15px;cursor:pointer">{WebUtility.HtmlEncode(buttonText)}</button>
            </form>
            </body></html>
            """;
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
