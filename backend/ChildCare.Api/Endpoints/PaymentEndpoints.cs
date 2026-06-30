using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ChildCare.Api.Data;
using ChildCare.Api.Models;
using ChildCare.Api.Services;

namespace ChildCare.Api.Endpoints;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/payments").WithTags("Payments");

        // GET /api/payments/status
        group.MapGet("/status", async (HttpContext ctx, AppDbContext db) =>
        {
            var userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();

            var user = await db.Users.FindAsync(Guid.Parse(userId));
            if (user is null) return Results.NotFound();

            return Results.Ok(new SubscriptionStatusResponse(
                user.SubscriptionStatus.ToString(),
                user.SubscriptionStatus is SubscriptionStatus.Active or SubscriptionStatus.Trialing,
                user.SubscriptionCurrentPeriodEnd));
        }).RequireAuthorization().RequireRateLimiting("api-user");

        // POST /api/payments/checkout — creates a Stripe Checkout session with 14-day trial
        // Accepts optional successUrl/cancelUrl so the web client can pass its own URLs.
        group.MapPost("/checkout", async (HttpContext ctx, AppDbContext db, StripeService stripe, CheckoutRequest? req) =>
        {
            var userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();

            var user = await db.Users.FindAsync(Guid.Parse(userId));
            if (user is null) return Results.NotFound();

            var url = await stripe.CreateCheckoutSessionUrlAsync(user, req?.SuccessUrl, req?.CancelUrl);
            return Results.Ok(new { url });
        }).RequireAuthorization().RequireRateLimiting("api-payment");

        // POST /api/payments/portal — opens the Stripe Customer Portal to manage subscription
        // Accepts optional returnUrl so the web client can pass its own URL.
        group.MapPost("/portal", async (HttpContext ctx, AppDbContext db, StripeService stripe, PortalRequest? req) =>
        {
            var userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();

            var user = await db.Users.FindAsync(Guid.Parse(userId));
            if (user is null) return Results.NotFound();

            var url = await stripe.CreatePortalSessionUrlAsync(user, req?.ReturnUrl);
            return Results.Ok(new { url });
        }).RequireAuthorization().RequireRateLimiting("api-payment");

        // POST /api/payments/webhook — called by Stripe, verified by signature (no JWT)
        group.MapPost("/webhook", async (HttpContext ctx, StripeService stripe) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var payload      = await reader.ReadToEndAsync();
            var signature    = ctx.Request.Headers["Stripe-Signature"].ToString();

            try
            {
                await stripe.HandleWebhookAsync(payload, signature);
                return Results.Ok();
            }
            catch (Stripe.StripeException)
            {
                return Results.BadRequest();
            }
        });
    }
}

public record SubscriptionStatusResponse(string Status, bool IsActive, DateTime? CurrentPeriodEnd);

public record CheckoutRequest(string? SuccessUrl, string? CancelUrl);

public record PortalRequest(string? ReturnUrl);
