using System.Security.Claims;
using ChildCare.Application.CodaTransactions;
using ChildCare.Domain.Enums;
using MediatR;

namespace ChildCare.Api.Endpoints;

// Feature 025 — contracts/coda-payment-matching-api.md. Director-only, mirrors
// InvoiceEndpoints' single-group-per-file pattern.
public static class CodaTransactionEndpoints
{
    public static void MapCodaTransactionEndpoints(this WebApplication app)
    {
        var director = app.MapGroup("/api").WithTags("CodaTransactions").RequireAuthorization("DirectorOnly");

        director.MapPost("/coda-imports", async (IFormFile file, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            await using var stream = file.OpenReadStream();
            var result = await mediator.Send(new ImportCodaFileCommand(stream, file.FileName, tenantUserId));
            if (!result.Succeeded)
                return Results.Json(new { errorKey = "errors.coda_import.invalid_file" }, statusCode: StatusCodes.Status422UnprocessableEntity);
            return Results.Ok(result.Response);
        })
        // Minimal APIs auto-require antiforgery for IFormFile-bound endpoints; this API has no
        // cookie/browser session for antiforgery to protect (JWT bearer auth only), so no
        // antiforgery middleware is registered and this must be explicitly opted out (mirrors
        // GroupActivityEndpoints' photo-upload route).
        .DisableAntiforgery();

        director.MapGet("/coda-transactions", async (string? matchType, bool? needsReview, IMediator mediator) =>
        {
            CodaMatchType? parsedMatchType = null;
            if (!string.IsNullOrEmpty(matchType))
            {
                if (!Enum.TryParse<CodaMatchType>(matchType, ignoreCase: true, out var parsed))
                    return Results.Json(new { errorKey = "errors.coda_transaction.invalid_match_type" }, statusCode: StatusCodes.Status422UnprocessableEntity);
                parsedMatchType = parsed;
            }

            var result = await mediator.Send(new ListCodaTransactionsQuery(parsedMatchType, needsReview));
            return Results.Ok(result);
        });

        director.MapPost("/coda-transactions/{id:guid}/confirm", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new ConfirmCodaTransactionMatchCommand(id));
            if (result.Succeeded)
                return Results.Ok(result.Response);
            return result.Failure switch
            {
                ConfirmCodaTransactionMatchFailure.NotFound => Results.Json(new { errorKey = "errors.coda_transaction.not_found" }, statusCode: StatusCodes.Status404NotFound),
                ConfirmCodaTransactionMatchFailure.NotConfirmable => Results.Json(new { errorKey = "errors.coda_transaction.not_confirmable" }, statusCode: StatusCodes.Status422UnprocessableEntity),
                _ => throw new InvalidOperationException($"Unhandled {nameof(ConfirmCodaTransactionMatchFailure)}: {result.Failure}"),
            };
        });

        director.MapPost("/coda-transactions/{id:guid}/reject", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new RejectCodaTransactionMatchCommand(id));
            if (result.Succeeded)
                return Results.Ok(result.Response);
            return result.Failure switch
            {
                RejectCodaTransactionMatchFailure.NotFound => Results.Json(new { errorKey = "errors.coda_transaction.not_found" }, statusCode: StatusCodes.Status404NotFound),
                RejectCodaTransactionMatchFailure.NotConfirmable => Results.Json(new { errorKey = "errors.coda_transaction.not_confirmable" }, statusCode: StatusCodes.Status422UnprocessableEntity),
                _ => throw new InvalidOperationException($"Unhandled {nameof(RejectCodaTransactionMatchFailure)}: {result.Failure}"),
            };
        });

        director.MapPost("/coda-transactions/{id:guid}/review", async (Guid id, HttpContext ctx, IMediator mediator) =>
        {
            var tenantUserId = Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await mediator.Send(new ReviewCodaTransactionCommand(id, tenantUserId));
            if (result.Succeeded)
                return Results.Ok(result.Response);
            return result.Failure switch
            {
                ReviewCodaTransactionFailure.NotFound => Results.Json(new { errorKey = "errors.coda_transaction.not_found" }, statusCode: StatusCodes.Status404NotFound),
                ReviewCodaTransactionFailure.NotReviewable => Results.Json(new { errorKey = "errors.coda_transaction.not_reviewable" }, statusCode: StatusCodes.Status422UnprocessableEntity),
                _ => throw new InvalidOperationException($"Unhandled {nameof(ReviewCodaTransactionFailure)}: {result.Failure}"),
            };
        });
    }
}
