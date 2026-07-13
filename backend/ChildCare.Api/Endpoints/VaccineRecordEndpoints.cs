using System.Security.Claims;
using ChildCare.Application.VaccineRecords;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Feature 013c — contracts/vaccine-health-records-api.md.</summary>
public static class VaccineRecordEndpoints
{
    private const int UploadUrlExpiresInSeconds = 900;

    public static void MapVaccineRecordEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/children/{childId:guid}/vaccine-records")
            .WithTags("VaccineRecords")
            .RequireAuthorization("DirectorOnly");

        group.MapPost("/", async (Guid childId, CreateVaccineRecordRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateVaccineRecordCommand(
                childId, req.VaccineName, req.VaccineTypeId, req.DoseNumber, req.AdministeredOn, req.NextDueDate,
                req.AdministeredBy, req.Notes, TenantUserIdOf(ctx)));
            return MapResult(result, onSuccess: r => Results.Created($"/api/children/{childId}/vaccine-records/{r.Id}", r));
        });

        group.MapGet("/", async (Guid childId, IMediator mediator) =>
        {
            var list = await mediator.Send(new ListChildVaccineRecordsQuery(childId));
            return Results.Ok(list);
        });

        group.MapPut("/{id:guid}", async (Guid childId, Guid id, UpdateVaccineRecordRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateVaccineRecordCommand(
                childId, id, req.VaccineName, req.VaccineTypeId, req.DoseNumber, req.AdministeredOn, req.NextDueDate,
                req.AdministeredBy, req.Notes));
            return MapResult(result, onSuccess: Results.Ok);
        });

        group.MapDelete("/{id:guid}", async (Guid childId, Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeleteVaccineRecordCommand(childId, id));
            return result.Succeeded
                ? Results.NoContent()
                : MapFailure(result.Failure!.Value);
        });

        group.MapPost("/{id:guid}/attachment-upload-url", async (Guid childId, Guid id, CreateVaccineRecordAttachmentUploadUrlRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateVaccineRecordAttachmentUploadUrlCommand(childId, id, req.ContentType));
            return result.Succeeded
                ? Results.Ok(new CreateVaccineRecordAttachmentUploadUrlResponse(result.UploadUrl!, UploadUrlExpiresInSeconds))
                : MapFailure(result.Failure!.Value);
        });

        var dashboard = app.MapGroup("/api/vaccine-records")
            .WithTags("VaccineRecords")
            .RequireAuthorization("DirectorOnly");

        dashboard.MapGet("/due-soon", async (int? withinDays, IMediator mediator) =>
        {
            var list = await mediator.Send(new ListVaccinationsDueSoonQuery(withinDays ?? 30));
            return Results.Ok(list);
        });
    }

    private static Guid TenantUserIdOf(HttpContext ctx) => Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private static IResult MapResult(VaccineRecordResult result, Func<VaccineRecordResponse, IResult> onSuccess) =>
        result.Succeeded ? onSuccess(result.Response!) : MapFailure(result.Failure!.Value);

    private static IResult MapFailure(VaccineRecordFailure failure) => failure switch
    {
        VaccineRecordFailure.ChildNotFound => Results.Json(
            new { errorKey = "errors.children.not_found" }, statusCode: StatusCodes.Status404NotFound),

        VaccineRecordFailure.NotFound => Results.Json(
            new { errorKey = "errors.vaccine_records.not_found" }, statusCode: StatusCodes.Status404NotFound),

        VaccineRecordFailure.VaccineTypeNotFound => Results.Json(
            new { errorKey = "errors.vaccine_records.vaccine_type_not_found" }, statusCode: StatusCodes.Status422UnprocessableEntity),

        VaccineRecordFailure.InvalidContentType => Results.Json(
            new { errorKey = "errors.vaccine_records.attachment_content_type_invalid" }, statusCode: StatusCodes.Status422UnprocessableEntity),

        _ => throw new InvalidOperationException($"Unhandled {nameof(VaccineRecordFailure)}: {failure}"),
    };
}
