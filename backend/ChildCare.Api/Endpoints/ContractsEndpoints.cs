using ChildCare.Application.Contracts;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Every route is DirectorOnly and non-tenant-exempt — mirrors ChildrenEndpoints.cs.</summary>
public static class ContractsEndpoints
{
    public static void MapContractsEndpoints(this WebApplication app)
    {
        var childContracts = app.MapGroup("/api/children/{childId:guid}/contracts")
            .WithTags("Contracts")
            .RequireAuthorization("DirectorOnly");

        var contracts = app.MapGroup("/api/contracts")
            .WithTags("Contracts")
            .RequireAuthorization("DirectorOnly");

        childContracts.MapGet("/", async (Guid childId, IMediator mediator) =>
        {
            var result = await mediator.Send(new ListChildContractsQuery(childId));
            return result.ChildFound
                ? Results.Ok(result.Contracts)
                : Results.Json(new { errorKey = "errors.child.not_found" }, statusCode: StatusCodes.Status404NotFound);
        });

        childContracts.MapPost("/", async (Guid childId, CreateContractRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateContractCommand(
                childId, req.LocationId, req.StartDate, req.EndDate, req.ContractedDays, req.DailyRateCents, req.Consent));
            return MapResult(result, onSuccess: r => Results.Created($"/api/contracts/{r.Id}", r));
        });

        contracts.MapGet("/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetContractByIdQuery(id));
            return MapResult(result, onSuccess: Results.Ok);
        });

        contracts.MapPut("/{id:guid}", async (Guid id, UpdateContractRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateContractCommand(
                id, req.StartDate, req.EndDate, req.ContractedDays, req.DailyRateCents, req.Consent));
            return MapResult(result, onSuccess: Results.Ok);
        });

        contracts.MapPost("/{id:guid}/activate", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new ActivateContractCommand(id));
            return MapResult(result, onSuccess: Results.Ok);
        });

        contracts.MapPost("/{id:guid}/amend", async (Guid id, AmendContractRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new AmendContractCommand(
                id, req.EffectiveStartDate, req.LocationId, req.EndDate, req.ContractedDays, req.DailyRateCents, req.Consent));
            return MapResult(result, onSuccess: r => Results.Created($"/api/contracts/{r.Id}", r));
        });

        contracts.MapPost("/{id:guid}/terminate", async (Guid id, TerminateContractRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new TerminateContractCommand(id, req.EndDate));
            return MapResult(result, onSuccess: Results.Ok);
        });

        contracts.MapGet("/{id:guid}/pdf", async (Guid id, string? locale, IMediator mediator) =>
        {
            var result = await mediator.Send(new GenerateContractPdfQuery(id, locale));
            return result.Found
                ? Results.File(result.Bytes, "application/pdf")
                : Results.Json(new { errorKey = "errors.contract.not_found" }, statusCode: StatusCodes.Status404NotFound);
        });
    }

    private static IResult MapResult(ContractResult result, Func<ContractResponse, IResult> onSuccess) =>
        result.Succeeded ? onSuccess(result.Response!) : MapFailure(result.Failure!.Value);

    private static IResult MapFailure(ContractFailure failure) => failure switch
    {
        ContractFailure.NotFound => Results.Json(
            new { errorKey = "errors.contract.not_found" },
            statusCode: StatusCodes.Status404NotFound),

        ContractFailure.ChildNotFound => Results.Json(
            new { errorKey = "errors.child.not_found" },
            statusCode: StatusCodes.Status404NotFound),

        // FR-004a, mirrors feature 006's CHK003 precedent — reused for both "doesn't exist"
        // and "exists but deactivated".
        ContractFailure.LocationNotFound => Results.Json(
            new { errorKey = "errors.location.not_found" },
            statusCode: StatusCodes.Status404NotFound),

        ContractFailure.NotDraft => Results.Json(
            new { errorKey = "errors.contract.not_draft" },
            statusCode: StatusCodes.Status409Conflict),

        ContractFailure.NotActive => Results.Json(
            new { errorKey = "errors.contract.not_active" },
            statusCode: StatusCodes.Status409Conflict),

        ContractFailure.AlreadyActiveAtLocation => Results.Json(
            new { errorKey = "errors.contract.already_active_at_location" },
            statusCode: StatusCodes.Status409Conflict),

        ContractFailure.DayOverlap => Results.Json(
            new { errorKey = "errors.contract.day_overlap" },
            statusCode: StatusCodes.Status409Conflict),

        ContractFailure.AmendmentStartDateInvalid => Results.Json(
            new { errorKey = "errors.contract.amendment_start_date_invalid" },
            statusCode: StatusCodes.Status422UnprocessableEntity),

        ContractFailure.TerminationDateInvalid => Results.Json(
            new { errorKey = "errors.contract.termination_date_invalid" },
            statusCode: StatusCodes.Status422UnprocessableEntity),

        _ => throw new InvalidOperationException($"Unhandled {nameof(ContractFailure)}: {failure}"),
    };
}
