using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.WaitingList;

public record UpdateWaitingListEntryCommand(
    Guid Id,
    string ChildFirstName,
    string ChildLastName,
    DateOnly DateOfBirth,
    string ContactName,
    string? ContactEmail,
    string? ContactPhone,
    Guid LocationId,
    DateOnly? RequestedStartDate,
    string? Notes) : IRequest<WaitingListEntryResult>;

public class UpdateWaitingListEntryCommandValidator : AbstractValidator<UpdateWaitingListEntryCommand>
{
    public UpdateWaitingListEntryCommandValidator()
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

public class UpdateWaitingListEntryCommandHandler(ITenantDbContext db) : IRequestHandler<UpdateWaitingListEntryCommand, WaitingListEntryResult>
{
    public async Task<WaitingListEntryResult> Handle(UpdateWaitingListEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await db.WaitingListEntries.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (entry is null)
            return WaitingListEntryResult.Fail(WaitingListFailure.NotFound);

        var locationExists = await db.Locations.AnyAsync(l => l.Id == request.LocationId && l.DeactivatedAt == null, cancellationToken);
        if (!locationExists)
            return WaitingListEntryResult.Fail(WaitingListFailure.LocationNotFound);

        entry.ChildFirstName = request.ChildFirstName.Trim();
        entry.ChildLastName = request.ChildLastName.Trim();
        entry.DateOfBirth = request.DateOfBirth;
        entry.ContactName = request.ContactName.Trim();
        entry.ContactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim();
        entry.ContactPhone = string.IsNullOrWhiteSpace(request.ContactPhone) ? null : request.ContactPhone.Trim();
        entry.LocationId = request.LocationId;
        entry.RequestedStartDate = request.RequestedStartDate;
        entry.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        entry.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var isDuplicate = await db.WaitingListEntries.AnyAsync(e =>
            e.LocationId == entry.LocationId &&
            e.Id != entry.Id &&
            e.ChildFirstName == entry.ChildFirstName &&
            e.ChildLastName == entry.ChildLastName &&
            e.DateOfBirth == entry.DateOfBirth,
            cancellationToken);

        return WaitingListEntryResult.Success(WaitingListMapper.ToResponse(entry, isDuplicate));
    }
}
