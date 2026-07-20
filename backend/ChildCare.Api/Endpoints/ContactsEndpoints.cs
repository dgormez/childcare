using System.Security.Claims;
using ChildCare.Application.Contacts;
using ChildCare.Contracts.Requests;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Enums;
using MediatR;

namespace ChildCare.Api.Endpoints;

/// <summary>Every route is DirectorOnly and non-tenant-exempt — mirrors ChildrenEndpoints.cs.</summary>
public static class ContactsEndpoints
{
    public static void MapContactsEndpoints(this WebApplication app)
    {
        var contacts = app.MapGroup("/api/contacts")
            .WithTags("Contacts")
            .RequireAuthorization("DirectorOnly");

        contacts.MapGet("/", async (IMediator mediator) =>
        {
            var list = await mediator.Send(new ListContactsQuery());
            return Results.Ok(list);
        });

        contacts.MapPost("/", async (CreateContactRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new CreateContactCommand(req.FirstName, req.LastName, req.Phone, req.Email, req.Locale));
            return MapContactResult(result, onSuccess: r => Results.Created($"/api/contacts/{r.Id}", r));
        });

        contacts.MapPut("/{id:guid}", async (Guid id, UpdateContactRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(new UpdateContactCommand(id, req.FirstName, req.LastName, req.Phone, req.Email, req.Locale));
            return MapContactResult(result, onSuccess: Results.Ok);
        });

        // Feature 022 FR-002/FR-005: create or correct a contact's identity verification.
        // VerifiedByUserId/VerifiedByEmail resolved server-side, same pattern as
        // ChildrenEndpoints.cs's ActingUserOf.
        contacts.MapPost("/{id:guid}/identity-verification", async (Guid id, VerifyContactIdentityRequest req, HttpContext ctx, IMediator mediator) =>
        {
            var (userId, email) = ActingUserOf(ctx);
            var documentType = IdDocumentTypeExtensions.TryParseWireString(req.DocumentType, out var parsed) ? parsed : (IdDocumentType?)null;
            var result = await mediator.Send(new VerifyContactIdentityCommand(id, documentType, req.Note, userId, email));
            return MapContactResult(result, onSuccess: Results.Ok);
        });

        var childContacts = app.MapGroup("/api/children/{childId:guid}/contacts")
            .WithTags("Contacts")
            .RequireAuthorization("DirectorOnly");

        // Feature 030 (US4) — new; see ListChildContactsQuery's doc comment.
        childContacts.MapGet("/", async (Guid childId, IMediator mediator) =>
        {
            var list = await mediator.Send(new ListChildContactsQuery(childId));
            return Results.Ok(list);
        });

        childContacts.MapPost("/", async (Guid childId, LinkContactToChildRequest req, IMediator mediator) =>
        {
            var relationship = Enum.Parse<ContactRelationship>(req.Relationship, ignoreCase: true);
            var result = await mediator.Send(new LinkContactToChildCommand(childId, req.ContactId, relationship, req.CanPickup, req.IsPrimary));
            return MapChildContactResult(result, onSuccess: r => Results.Created($"/api/children/{childId}/contacts/{r.ContactId}", r));
        });

        childContacts.MapPut("/{contactId:guid}", async (Guid childId, Guid contactId, UpdateChildContactLinkRequest req, IMediator mediator) =>
        {
            var relationship = Enum.Parse<ContactRelationship>(req.Relationship, ignoreCase: true);
            var result = await mediator.Send(new UpdateChildContactLinkCommand(childId, contactId, relationship, req.CanPickup, req.IsPrimary));
            return MapChildContactResult(result, onSuccess: Results.Ok);
        });

        childContacts.MapDelete("/{contactId:guid}", async (Guid childId, Guid contactId, IMediator mediator) =>
        {
            var result = await mediator.Send(new UnlinkContactFromChildCommand(childId, contactId));
            if (!result.Succeeded)
                return MapContactFailure(result.Failure!.Value);
            return Results.Ok();
        });
    }

    // Feature 022 — mirrors ChildrenEndpoints.cs's ActingUserOf / PlatformAdminVaccineTypeEndpoints.cs
    // (013h). Only called on DirectorOnly routes, so both claims are always present.
    private static (Guid UserId, string Email) ActingUserOf(HttpContext ctx) => (
        Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)!.Value),
        ctx.User.FindFirst(ClaimTypes.Email)!.Value);

    private static IResult MapContactResult(ContactResult result, Func<ContactResponse, IResult> onSuccess) =>
        result.Succeeded ? onSuccess(result.Response!) : MapContactFailure(result.Failure!.Value);

    private static IResult MapChildContactResult(ChildContactResult result, Func<ChildContactResponse, IResult> onSuccess) =>
        result.Succeeded ? onSuccess(result.Response!) : MapContactFailure(result.Failure!.Value);

    private static IResult MapContactFailure(ContactFailure failure) => failure switch
    {
        ContactFailure.NotFound => Results.Json(
            new { errorKey = "errors.contact.not_found" },
            statusCode: StatusCodes.Status404NotFound),

        ContactFailure.ChildNotFound => Results.Json(
            new { errorKey = "errors.child.not_found" },
            statusCode: StatusCodes.Status404NotFound),

        ContactFailure.LinkAlreadyExists => Results.Json(
            new { errorKey = "errors.contact.link_already_exists" },
            statusCode: StatusCodes.Status409Conflict),

        _ => throw new InvalidOperationException($"Unhandled {nameof(ContactFailure)}: {failure}"),
    };
}
