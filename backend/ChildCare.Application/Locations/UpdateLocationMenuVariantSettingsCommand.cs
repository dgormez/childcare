using ChildCare.Application.Common;
using ChildCare.Application.MonthlyMenus;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Locations;

/// <summary>
/// FR-001/FR-002/FR-014. Removal-warning shape mirrors UpdateLocationReservationSettingsCommand's
/// ConfirmDespitePending pattern (013f) exactly: check what a removal would strand (here,
/// published content parents currently see), require explicit confirmation if anything is.
/// Re-enabling a previously-removed DietaryType always appends it at the end (FR-002) — the
/// incoming list order is taken as the new priority order verbatim, so "append at end" is simply
/// "the caller puts it last," not special-cased server-side.
/// </summary>
public record UpdateLocationMenuVariantSettingsCommand(
    Guid LocationId,
    List<string> MenuVariantPriorityOrder,
    bool ConfirmDespiteRemovingPublished) : IRequest<LocationResult>;

public class UpdateLocationMenuVariantSettingsCommandValidator : AbstractValidator<UpdateLocationMenuVariantSettingsCommand>
{
    public UpdateLocationMenuVariantSettingsCommandValidator()
    {
        RuleFor(x => x.LocationId).NotEmpty();

        RuleForEach(x => x.MenuVariantPriorityOrder)
            .Must(v => DietaryTypeExtensions.TryParseWireString(v, out _))
            .WithMessage("errors.location.menu_variant_settings.invalid_dietary_type");

        RuleFor(x => x.MenuVariantPriorityOrder)
            .Must(order => order.Distinct().Count() == order.Count)
            .WithMessage("errors.location.menu_variant_settings.duplicate_dietary_type");
    }
}

public class UpdateLocationMenuVariantSettingsCommandHandler(ITenantDbContext db)
    : IRequestHandler<UpdateLocationMenuVariantSettingsCommand, LocationResult>
{
    public async Task<LocationResult> Handle(UpdateLocationMenuVariantSettingsCommand request, CancellationToken cancellationToken)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == request.LocationId, cancellationToken);
        if (location is null)
            return LocationResult.Fail(LocationFailure.NotFound);

        // Re-normalized through parse+ToWireString (not the raw incoming strings) so casing/
        // whitespace variance from a client never leaks into storage — the validator above
        // already guarantees every entry parses successfully.
        var newOrder = request.MenuVariantPriorityOrder
            .Select(v => DietaryTypeExtensions.TryParseWireString(v, out var parsed) ? parsed.ToWireString() : null)
            .Where(v => v is not null)
            .Select(v => v!)
            .ToList();

        if (!request.ConfirmDespiteRemovingPublished)
        {
            var beingRemoved = location.MenuVariantPriorityOrder.Except(newOrder).ToList();
            if (beingRemoved.Count > 0)
            {
                var removedWithPublishedContent = await MonthlyMenuVariantHelper.GetVariantsWithPublishedContentAsync(
                    db, location.Id, beingRemoved, cancellationToken);
                if (removedWithPublishedContent.Count > 0)
                    return LocationResult.FailMenuVariantRemoval(removedWithPublishedContent);
            }
        }

        location.MenuVariantPriorityOrder = newOrder;
        location.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var menuVariantsWithPublishedContent = await MonthlyMenuVariantHelper.GetVariantsWithPublishedContentAsync(
            db, location.Id, location.MenuVariantPriorityOrder, cancellationToken);

        return LocationResult.Success(LocationMapper.ToResponse(location, menuVariantsWithPublishedContent));
    }
}
