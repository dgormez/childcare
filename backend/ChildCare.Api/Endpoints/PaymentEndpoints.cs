using System.Security.Claims;
using ChildCare.Api.Middleware;
using ChildCare.Application.Payments;
using ChildCare.Contracts.Requests;
using MediatR;

namespace ChildCare.Api.Endpoints;

// Feature 014a — contracts/014a-invoice-payments-plus/payments-api.md. Director/parent routes
// mirror InvoiceEndpoints' MapGroup pattern; the webhook route is this feature's one
// TenantExempt (public) route (research.md R2).
public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var director = app.MapGroup("/api/organisations/me").WithTags("Payments").RequireAuthorization("DirectorOnly");

        director.MapGet("/payment-connection", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetPaymentConnectionStatusQuery());
            return Results.Ok(new { status = result.Status, providerAccountLabel = result.ProviderAccountLabel, connectedAt = result.ConnectedAt });
        });

        director.MapPost("/payment-connection/authorize", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new ConnectPaymentProviderCommand());
            return Results.Ok(new { authorizationUrl = result.AuthorizationUrl });
        });

        director.MapPost("/payment-connection/callback", async (CompletePaymentConnectionRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CompletePaymentProviderOAuthCommand(req.AuthorizationCode));
            if (!result.Succeeded)
                return Results.Json(new { errorKey = "errors.paymentConnection.oauth_failed" }, statusCode: StatusCodes.Status422UnprocessableEntity);
            return Results.Ok(new { status = "connected", providerAccountLabel = result.ProviderAccountLabel });
        });

        director.MapDelete("/payment-connection", async (IMediator mediator) =>
        {
            await mediator.Send(new DisconnectPaymentProviderCommand());
            return Results.NoContent();
        });

        var parent = app.MapGroup("/api/parent").WithTags("Payments").RequireAuthorization("ParentOnly");

        parent.MapPost("/invoices/{id:guid}/payment-link", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new CreatePaymentLinkCommand(tenantUserId, id));
            if (result.Succeeded)
                return Results.Ok(new { checkoutUrl = result.CheckoutUrl });
            return result.Failure switch
            {
                CreatePaymentLinkFailure.InvoiceNotFound => Results.Json(new { errorKey = "errors.invoice.not_found" }, statusCode: StatusCodes.Status404NotFound),
                CreatePaymentLinkFailure.InvoiceNotSent => Results.Json(new { errorKey = "errors.invoice.not_sent" }, statusCode: StatusCodes.Status422UnprocessableEntity),
                CreatePaymentLinkFailure.ProviderNotConnected => Results.Json(new { errorKey = "errors.paymentConnection.not_connected" }, statusCode: StatusCodes.Status422UnprocessableEntity),
                _ => throw new InvalidOperationException($"Unhandled {nameof(CreatePaymentLinkFailure)}: {result.Failure}"),
            };
        });

        parent.MapGet("/invoices/{id:guid}/payment-status", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new GetPaymentStatusQuery(tenantUserId, id));
            if (!result.Found)
                return Results.Json(new { errorKey = "errors.invoice.not_found" }, statusCode: StatusCodes.Status404NotFound);
            return Results.Ok(new { invoiceStatus = result.InvoiceStatus, paymentStatus = result.PaymentStatus });
        });

        parent.MapGet("/invoices/{id:guid}/betalingsbewijs", async (Guid id, HttpContext ctx, IMediator mediator, string? locale) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new GenerateBetalingsbewijsQuery(tenantUserId, id, locale));
            if (!result.Found)
                return Results.Json(new { errorKey = "errors.invoice.not_found" }, statusCode: StatusCodes.Status404NotFound);
            return Results.File(result.Bytes, "application/pdf", $"betalingsbewijs-{id}.pdf");
        });

        // Public webhook (research.md R2) — resolves tenant/invoice solely from the opaque
        // PaymentReference path segment this system generated, never from the request body.
        var webhook = app.MapGroup("/api/webhooks").WithTags("Payments");

        webhook.MapPost("/mollie/{paymentReference:guid}", async (Guid paymentReference, IMediator mediator) =>
        {
            await mediator.Send(new ProcessPaymentWebhookCommand(paymentReference));
            // Always 200 regardless of resolution outcome (spec.md Edge Cases — no
            // tenant-enumeration oracle; Mollie's own webhook-retry contract expects 2xx once
            // processing has been attempted).
            return Results.Ok();
        }).RequireTenantExempt();
    }
}
