using ChildCare.Application.Children;
using ChildCare.Application.Common;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.WaitingList;

/// <summary>
/// FR-010/FR-011/FR-012. Exactly one of ChildId or CreateNewChild must be provided.
/// </summary>
public record LinkChildToWaitingListEntryCommand(Guid Id, Guid? ChildId, bool CreateNewChild) : IRequest<WaitingListEntryResult>;

public class LinkChildToWaitingListEntryCommandValidator : AbstractValidator<LinkChildToWaitingListEntryCommand>
{
    public LinkChildToWaitingListEntryCommandValidator()
    {
        RuleFor(x => x).Must(x => x.ChildId.HasValue != x.CreateNewChild)
            .WithMessage("errors.validation");
    }
}

public class LinkChildToWaitingListEntryCommandHandler(ITenantDbContext db, IMediator mediator)
    : IRequestHandler<LinkChildToWaitingListEntryCommand, WaitingListEntryResult>
{
    public async Task<WaitingListEntryResult> Handle(LinkChildToWaitingListEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await db.WaitingListEntries.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);
        if (entry is null)
            return WaitingListEntryResult.Fail(WaitingListFailure.NotFound);

        Guid childId;
        if (request.CreateNewChild)
        {
            // R5: reuse feature 006's CreateChildCommand as-is, pre-filled from the entry —
            // no new child-creation logic is written for this feature.
            var createResult = await mediator.Send(
                new CreateChildCommand(entry.ChildFirstName, entry.ChildLastName, entry.DateOfBirth,
                    Gender: null, Nationality: null, AllergiesDescription: null, AllergySeverity: null,
                    MedicalConditions: null, DietaryRestrictions: null, GpName: null, GpPhone: null,
                    PediatricianName: null, PediatricianPhone: null,
                    HealthInsuranceNumber: null, Kindcode: null),
                cancellationToken);
            childId = createResult.Response!.Id;
        }
        else
        {
            var childExists = await db.Children.AnyAsync(c => c.Id == request.ChildId, cancellationToken);
            if (!childExists)
                return WaitingListEntryResult.Fail(WaitingListFailure.ChildNotFound);

            childId = request.ChildId!.Value;
        }

        entry.ChildId = childId;
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
