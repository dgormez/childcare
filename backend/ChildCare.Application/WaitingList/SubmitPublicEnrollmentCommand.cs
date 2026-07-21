using ChildCare.Application.Auth;
using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.WaitingList;

public enum PublicEnrollmentFailure
{
    NotFound,
    Disabled,
}

public class SubmitPublicEnrollmentResult
{
    public bool Succeeded { get; init; }
    public PublicEnrollmentFailure? Failure { get; init; }
    public string? ReferenceCode { get; init; }

    public static SubmitPublicEnrollmentResult Success(string referenceCode) => new() { Succeeded = true, ReferenceCode = referenceCode };
    public static SubmitPublicEnrollmentResult Fail(PublicEnrollmentFailure failure) => new() { Failure = failure };
}

/// <summary>
/// FR-007/FR-008/FR-009/FR-010/FR-013/FR-021. The honeypot check (FR-005) happens in
/// PublicEnrollmentEndpoints before this command is ever dispatched — a filled honeypot must
/// create nothing, so it never reaches here at all (contracts/enrollment-api.md).
/// </summary>
public record SubmitPublicEnrollmentCommand(
    string OrganisationSlug,
    string LocationSlug,
    string ChildFirstName,
    string ChildLastName,
    DateOnly DateOfBirth,
    DateOnly? RequestedStartDate,
    string ContactName,
    string ContactEmail,
    string? ContactPhone,
    string? Notes,
    string Locale) : IRequest<SubmitPublicEnrollmentResult>;

public class SubmitPublicEnrollmentCommandValidator : AbstractValidator<SubmitPublicEnrollmentCommand>
{
    public SubmitPublicEnrollmentCommandValidator()
    {
        // Cascade(CascadeMode.Stop) on every chain (CreateContactCommandValidator's precedent):
        // the global exception handler builds fieldErrors via a plain PropertyName -> single
        // ErrorMessage dictionary, which throws on a duplicate key if more than one rule for the
        // same property fails at once.
        RuleFor(x => x.ChildFirstName).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ChildLastName).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DateOfBirth).LessThanOrEqualTo(_ => BelgianCalendarDay.Today());
        RuleFor(x => x.ContactName).Cascade(CascadeMode.Stop).NotEmpty().MaximumLength(200);
        // FR-003/data-model.md's validation delta from 012a: contact email is required for a
        // self-registered submission — it's the only delivery channel for the confirmation and
        // reference code (unlike 012a's director-entered flow, where it stays optional).
        RuleFor(x => x.ContactEmail).Cascade(CascadeMode.Stop).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.Locale).Must(l => l is "nl" or "fr" or "en").WithMessage("errors.validation");
    }
}

public class SubmitPublicEnrollmentCommandHandler(
    OrganisationSlugResolver slugResolver,
    ITenantDbContextResolver tenantResolver,
    IEmailSender emailSender,
    EnrollmentNotificationService notificationService) : IRequestHandler<SubmitPublicEnrollmentCommand, SubmitPublicEnrollmentResult>
{
    public async Task<SubmitPublicEnrollmentResult> Handle(SubmitPublicEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var tenant = await slugResolver.ResolveAsync(request.OrganisationSlug, cancellationToken);
        if (tenant is null)
            return SubmitPublicEnrollmentResult.Fail(PublicEnrollmentFailure.NotFound);

        var db = tenantResolver.ForSchema(tenant.SchemaName);

        var location = await db.Locations.FirstOrDefaultAsync(
            l => l.PublicEnrollmentSlug == request.LocationSlug && l.DeactivatedAt == null, cancellationToken);
        if (location is null)
            return SubmitPublicEnrollmentResult.Fail(PublicEnrollmentFailure.NotFound);

        // FR-013: enforced here too, not just hidden by the public page — a direct request
        // against a disabled location must still be rejected server-side.
        if (!location.PublicEnrollmentEnabled)
            return SubmitPublicEnrollmentResult.Fail(PublicEnrollmentFailure.Disabled);

        var referenceCode = await GenerateUniqueReferenceCodeAsync(db, cancellationToken);

        // FR-002 (012a): appended after all existing entries for this location, same ordering
        // rule CreateWaitingListEntryCommandHandler uses for director-entered entries.
        var maxPriority = await db.WaitingListEntries
            .Where(e => e.LocationId == location.Id)
            .Select(e => (int?)e.Priority)
            .MaxAsync(cancellationToken);

        var entry = new WaitingListEntry
        {
            ChildFirstName = request.ChildFirstName.Trim(),
            ChildLastName = request.ChildLastName.Trim(),
            DateOfBirth = request.DateOfBirth,
            ContactName = request.ContactName.Trim(),
            ContactEmail = request.ContactEmail.Trim(),
            ContactPhone = string.IsNullOrWhiteSpace(request.ContactPhone) ? null : request.ContactPhone.Trim(),
            LocationId = location.Id,
            RequestedStartDate = request.RequestedStartDate,
            Priority = (maxPriority ?? -1) + 1,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            Source = WaitingListEntrySource.SelfRegistered,
            ReferenceCode = referenceCode,
            SubmittedLocale = request.Locale,
        };

        db.WaitingListEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        await emailSender.SendEnrollmentConfirmationAsync(
            entry.ContactEmail!, request.Locale, $"{entry.ChildFirstName} {entry.ChildLastName}", location.Name, referenceCode, cancellationToken);

        await notificationService.NotifyDirectorsAsync(db, entry, cancellationToken);

        return SubmitPublicEnrollmentResult.Success(referenceCode);
    }

    private static async Task<string> GenerateUniqueReferenceCodeAsync(ITenantDbContext db, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = ReferenceCodeGenerator.Generate();
            var exists = await db.WaitingListEntries.AnyAsync(e => e.ReferenceCode == candidate, cancellationToken);
            if (!exists)
                return candidate;
        }

        throw new InvalidOperationException("Could not generate a unique reference code after 10 attempts.");
    }
}
