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

        var childContacts = app.MapGroup("/api/children/{childId:guid}/contacts")
            .WithTags("Contacts")
            .RequireAuthorization("DirectorOnly");

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
