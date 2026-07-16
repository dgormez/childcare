using System.Security.Claims;
using ChildCare.Application.Locations;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>
/// The first tenant-domain-data endpoint group (constitution Principle I) — deliberately NOT
/// tenant-exempt, so TenantMiddleware (feature 002) resolves ICurrentTenantService/ITenantDbContext
/// before any handler below runs. Group-level RequireAuthorization("DirectorOnly") covers every
/// route mapped inside it (list, get, create, update, deactivate, reactivate, duplicate — FR-011).
/// </summary>
public static class LocationEndpoints
{
    public static void MapLocationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/locations")
            .WithTags("Locations")
            .RequireAuthorization("DirectorOnly");

        group.MapGet("/", async (IMediator mediator, bool includeDeactivated = false) =>
        {
            var locations = await mediator.Send(new ListLocationsQuery(includeDeactivated));
            return Results.Ok(locations);
        });

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetLocationByIdQuery(id));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPost("/", async (CreateLocationRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateLocationCommand(
                req.Name, req.Address, req.Phone, req.Email, req.MaxCapacity));
            return MapResult(result, onSuccess: r => Results.Created($"/api/locations/{r.Id}", r));
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateLocationRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateLocationCommand(
                id, req.Name, req.Address, req.Phone, req.Email, req.MaxCapacity,
                req.NaamLocatie, req.Dossiernummer, req.Verantwoordelijke, req.FlexPermission, req.BoPermission));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPut("/{id:guid}/reservation-settings", async (Guid id, UpdateLocationReservationSettingsRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateLocationReservationSettingsCommand(
                id, req.AbsencesMode, req.ExtrasMode, req.SwapsMode, req.NoticeHours, req.ConfirmDespitePending));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPut("/{id:guid}/checkin-settings", async (Guid id, UpdateLocationCheckInSettingsRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var directorId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new UpdateLocationCheckInSettingsCommand(id, directorId, req.RequiresCaregiverPin));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPut("/{id:guid}/menu-variant-settings", async (Guid id, UpdateLocationMenuVariantSettingsRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateLocationMenuVariantSettingsCommand(
                id, req.MenuVariantPriorityOrder.ToList(), req.ConfirmDespiteRemovingPublished));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPut("/{id:guid}/invoice-settings", async (Guid id, UpdateLocationInvoiceSettingsRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateLocationInvoiceSettingsCommand(
                id, req.Erkenningsnummer, req.BankAccountNumber, req.InvoiceDueDays));
            return MapResult(result, onSuccess: Results.Ok);
        });

        // Feature 014a — contracts/014a-invoice-payments-plus/payments-api.md.
        group.MapPut("/{id:guid}/payment-reminder-settings", async (Guid id, UpdateLocationPaymentReminderSettingsRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateLocationPaymentReminderSettingsCommand(
                id, req.Enabled, req.DelayDays, req.CadenceDays));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPost("/{id:guid}/deactivate", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeactivateLocationCommand(id));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPost("/{id:guid}/reactivate", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new ReactivateLocationCommand(id));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapPost("/{id:guid}/duplicate", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DuplicateLocationCommand(id));
            return MapResult(result, onSuccess: r => Results.Created($"/api/locations/{r.Id}", r));
        });
    }

    private static IResult MapResult(LocationResult result, Func<LocationResponse, IResult> onSuccess)
    {
        if (result.Succeeded)
            return onSuccess(result.Response!);

        return result.Failure switch
        {
            LocationFailure.NotFound => Results.Json(
                new { errorKey = "errors.location.not_found" },
                statusCode: StatusCodes.Status404NotFound),

            LocationFailure.HasActiveDependents => Results.Json(
                new { errorKey = "errors.location.has_active_dependents" },
                statusCode: StatusCodes.Status409Conflict),

            LocationFailure.PendingRequestsWarning => Results.Json(
                new { errorKey = "errors.location.reservation_settings.pending_requests_warning", pendingCounts = result.PendingCounts },
                statusCode: StatusCodes.Status409Conflict),

            LocationFailure.MenuVariantRemovalWarning => Results.Json(
                new { errorKey = "errors.location.menu_variant_settings.removing_published_warning", variants = result.VariantsRequiringConfirmation },
                statusCode: StatusCodes.Status409Conflict),

            _ => throw new InvalidOperationException($"Unhandled {nameof(LocationFailure)}: {result.Failure}"),
        };
    }
}
