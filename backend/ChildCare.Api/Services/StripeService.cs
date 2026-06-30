using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using ChildCare.Api.Data;
using ChildCare.Api.Models;

namespace ChildCare.Api.Services;

public class StripeService(IConfiguration config, AppDbContext db)
{
    private readonly string _webhookSecret = config["Stripe:WebhookSecret"] ?? "";
    private readonly string _priceId       = config["Stripe:PriceId"]       ?? "";
    private readonly string _successUrl    = config["Stripe:SuccessUrl"]    ?? "childcare://payment-success";
    private readonly string _cancelUrl     = config["Stripe:CancelUrl"]     ?? "childcare://payment-cancel";

    public async Task<string> GetOrCreateCustomerIdAsync(User user)
    {
        if (user.StripeCustomerId is not null)
            return user.StripeCustomerId;

        var customer = await new CustomerService().CreateAsync(new CustomerCreateOptions
        {
            Email    = user.Email,
            Metadata = new Dictionary<string, string> { ["user_id"] = user.Id.ToString() },
        });

        user.StripeCustomerId = customer.Id;
        await db.SaveChangesAsync();
        return customer.Id;
    }

    /// <param name="successUrl">Override the default success URL (e.g. pass a web URL from the web client).</param>
    /// <param name="cancelUrl">Override the default cancel URL.</param>
    public async Task<string> CreateCheckoutSessionUrlAsync(User user, string? successUrl = null, string? cancelUrl = null)
    {
        var customerId = await GetOrCreateCustomerIdAsync(user);
        var success    = successUrl ?? _successUrl;
        var cancel     = cancelUrl  ?? _cancelUrl;

        var session = await new SessionService().CreateAsync(new SessionCreateOptions
        {
            Customer           = customerId,
            Mode               = "subscription",
            PaymentMethodTypes = ["card"],
            LineItems          = [new SessionLineItemOptions { Price = _priceId, Quantity = 1 }],
            SubscriptionData   = new SessionSubscriptionDataOptions { TrialPeriodDays = 14 },
            SuccessUrl         = $"{success}?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl          = cancel,
        });

        return session.Url;
    }

    /// <param name="returnUrl">Override the default return URL (e.g. pass a web URL from the web client).</param>
    public async Task<string> CreatePortalSessionUrlAsync(User user, string? returnUrl = null)
    {
        var customerId = await GetOrCreateCustomerIdAsync(user);

        var session = await new Stripe.BillingPortal.SessionService().CreateAsync(
            new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer  = customerId,
                ReturnUrl = returnUrl ?? _cancelUrl,
            });

        return session.Url;
    }

    public async Task HandleWebhookAsync(string payload, string signature)
    {
        var stripeEvent = EventUtility.ConstructEvent(payload, signature, _webhookSecret);

        switch (stripeEvent.Type)
        {
            case EventTypes.CustomerSubscriptionCreated:
            case EventTypes.CustomerSubscriptionUpdated:
            case EventTypes.CustomerSubscriptionDeleted:
                await SyncSubscriptionAsync((Subscription)stripeEvent.Data.Object);
                break;
        }
    }

    private async Task SyncSubscriptionAsync(Subscription sub)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.StripeCustomerId == sub.CustomerId);
        if (user is null) return;

        user.StripeSubscriptionId         = sub.Id;
        user.SubscriptionCurrentPeriodEnd = sub.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd;
        user.SubscriptionStatus = sub.Status switch
        {
            "active"   => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trialing,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" => SubscriptionStatus.Canceled,
            _          => SubscriptionStatus.None,
        };

        await db.SaveChangesAsync();
    }
}
