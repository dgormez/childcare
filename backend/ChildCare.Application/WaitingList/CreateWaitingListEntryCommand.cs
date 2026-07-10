using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.WaitingList;

public record CreateWaitingListEntryCommand(
    string ChildFirstName,
    string ChildLastName,
    DateOnly DateOfBirth,
    string ContactName,
    string? ContactEmail,
    string? ContactPhone,
    Guid LocationId,
    DateOnly? RequestedStartDate,
    string? Notes) : IRequest<WaitingListEntryResult>;

public class CreateWaitingListEntryCommandValidator : AbstractValidator<CreateWaitingListEntryCommand>
{
    public CreateWaitingListEntryCommandValidator()
    {
        RuleFor(x => x.ChildFirstName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ChildLastName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DateOfBirth).LessThanOrEqualTo(_ => BelgianCalendarDay.Today());
        RuleFor(x => x.ContactName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
        RuleFor(x => x.LocationId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(2000);
    }
}

public class CreateWaitingListEntryCommandHandler(ITenantDbContext db) : IRequestHandler<CreateWaitingListEntryCommand, WaitingListEntryResult>
{
    public async Task<WaitingListEntryResult> Handle(CreateWaitingListEntryCommand request, CancellationToken cancellationToken)
    {
        var locationExists = await db.Locations.AnyAsync(l => l.Id == request.LocationId && l.DeactivatedAt == null, cancellationToken);
        if (!locationExists)
            return WaitingListEntryResult.Fail(WaitingListFailure.LocationNotFound);

        // FR-002: appended after all existing entries for this location (lower priority value = higher rank).
        var maxPriority = await db.WaitingListEntries
            .Where(e => e.LocationId == request.LocationId)
            .Select(e => (int?)e.Priority)
            .MaxAsync(cancellationToken);

        var entry = new WaitingListEntry
        {
            ChildFirstName = request.ChildFirstName.Trim(),
            ChildLastName = request.ChildLastName.Trim(),
            DateOfBirth = request.DateOfBirth,
            ContactName = request.ContactName.Trim(),
            ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim(),
            ContactPhone = string.IsNullOrWhiteSpace(request.ContactPhone) ? null : request.ContactPhone.Trim(),
            LocationId = request.LocationId,
            RequestedStartDate = request.RequestedStartDate,
            Priority = (maxPriority ?? -1) + 1,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };

        db.WaitingListEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        // FR-004: duplicate detection compares against the full location roster regardless of status.
        var isDuplicate = await db.WaitingListEntries.AnyAsync(e =>
            e.LocationId == request.LocationId &&
            e.Id != entry.Id &&
            e.ChildFirstName == entry.ChildFirstName &&
            e.ChildLastName == entry.ChildLastName &&
            e.DateOfBirth == entry.DateOfBirth,
            cancellationToken);

        return WaitingListEntryResult.Success(WaitingListMapper.ToResponse(entry, isDuplicate));
    }
}
