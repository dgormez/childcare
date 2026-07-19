using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.Email;

public record SendBulkEmailCommand(
    Guid SentByTenantUserId,
    Guid LocationId,
    Guid? GroupId,
    string Subject,
    string Body,
    string? AttachmentObjectPath,
    string? AttachmentFileName,
    string? AttachmentContentType) : IRequest<SendBulkEmailResult>;

public enum SendBulkEmailFailure
{
    AttachmentTooLarge,
}

public class SendBulkEmailResult
{
    public Guid BulkEmailSendId { get; private init; }
    public int SentCount { get; private init; }
    public int SkippedNoEmailCount { get; private init; }
    public int ProviderFailureCount { get; private init; }
    public SendBulkEmailFailure? Failure { get; private init; }

    public bool Succeeded => Failure is null;

    public static SendBulkEmailResult Success(Guid bulkEmailSendId, int sent, int skipped, int failed) =>
        new() { BulkEmailSendId = bulkEmailSendId, SentCount = sent, SkippedNoEmailCount = skipped, ProviderFailureCount = failed };

    public static SendBulkEmailResult Fail(SendBulkEmailFailure failure) => new() { Failure = failure };
}

public class SendBulkEmailCommandValidator : AbstractValidator<SendBulkEmailCommand>
{
    public SendBulkEmailCommandValidator(ITenantDbContext db)
    {
        RuleFor(x => x.Subject)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.email.subject_required")
            .MaximumLength(200).WithMessage("errors.email.subject_too_long");

        RuleFor(x => x.Body)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("errors.email.body_required")
            .MaximumLength(5000).WithMessage("errors.email.body_too_long");

        RuleFor(x => x.LocationId)
            .MustAsync(async (locationId, ct) => await db.Locations.AnyAsync(l => l.Id == locationId, ct))
            .WithMessage("errors.location.not_found");

        RuleFor(x => x)
            .MustAsync(async (command, ct) => command.GroupId is null || await db.Groups.AnyAsync(g => g.Id == command.GroupId && g.LocationId == command.LocationId, ct))
            .WithMessage("errors.group.not_found");

        RuleFor(x => x)
            .Must(command => command.AttachmentObjectPath is null
                || (command.AttachmentFileName is not null && command.AttachmentContentType is not null))
            .WithMessage("errors.email.invalid_content_type");
    }
}

/// <summary>
/// FR-001/FR-002/FR-003/FR-012: sends one email per household (BulkEmailRecipientResolver
/// already de-duplicates by Contact), tolerates partial failure per recipient, and records one
/// BulkEmailRecipient audit row per outcome (data-model.md, research.md R6) backing the
/// director-facing delivery-outcome summary. A zero-recipient scope completes with all-zero
/// counts, not an error (FR-016).
/// </summary>
public class SendBulkEmailCommandHandler(
    ITenantDbContext db,
    IEmailSender emailSender,
    IBulkEmailAttachmentStorage attachmentStorage,
    ILogger<SendBulkEmailCommandHandler> logger) : IRequestHandler<SendBulkEmailCommand, SendBulkEmailResult>
{
    private const long MaxAttachmentSizeBytes = 10 * 1024 * 1024;

    public async Task<SendBulkEmailResult> Handle(SendBulkEmailCommand request, CancellationToken cancellationToken)
    {
        (byte[] Bytes, string FileName, string ContentType)? attachment = null;
        if (request.AttachmentObjectPath is not null)
        {
            var bytes = await attachmentStorage.DownloadBytesAsync(request.AttachmentObjectPath, cancellationToken);
            if (bytes.LongLength > MaxAttachmentSizeBytes)
                return SendBulkEmailResult.Fail(SendBulkEmailFailure.AttachmentTooLarge);

            attachment = (bytes, request.AttachmentFileName!, request.AttachmentContentType!);
        }

        var bulkEmailSend = new BulkEmailSend
        {
            LocationId = request.LocationId,
            GroupId = request.GroupId,
            Subject = request.Subject,
            Body = request.Body,
            AttachmentObjectPath = request.AttachmentObjectPath,
            AttachmentFileName = request.AttachmentFileName,
            AttachmentContentType = request.AttachmentContentType,
            SentByTenantUserId = request.SentByTenantUserId,
        };
        db.BulkEmailSends.Add(bulkEmailSend);

        var contacts = await BulkEmailRecipientResolver.ResolveAllContactsAsync(db, request.LocationId, request.GroupId, cancellationToken);

        var sentCount = 0;
        var skippedCount = 0;
        var failedCount = 0;

        foreach (var contact in contacts)
        {
            if (contact.Email is null)
            {
                db.BulkEmailRecipients.Add(new BulkEmailRecipient { BulkEmailSendId = bulkEmailSend.Id, ContactId = contact.Id, Status = BulkEmailDeliveryStatus.SkippedNoEmail });
                skippedCount++;
                logger.LogInformation("Bulk email {BulkEmailSendId}: contact {ContactId} has no email on file, skipped.", bulkEmailSend.Id, contact.Id);
                continue;
            }

            try
            {
                await emailSender.SendBulkEmailAsync(contact.Email, contact.Locale, request.Subject, request.Body, attachment, cancellationToken);
                db.BulkEmailRecipients.Add(new BulkEmailRecipient { BulkEmailSendId = bulkEmailSend.Id, ContactId = contact.Id, Status = BulkEmailDeliveryStatus.Sent });
                sentCount++;
            }
            catch (Exception ex)
            {
                db.BulkEmailRecipients.Add(new BulkEmailRecipient { BulkEmailSendId = bulkEmailSend.Id, ContactId = contact.Id, Status = BulkEmailDeliveryStatus.ProviderFailure, Error = ex.GetType().Name });
                failedCount++;
                logger.LogWarning(ex, "Bulk email {BulkEmailSendId}: dispatch failed for contact {ContactId}.", bulkEmailSend.Id, contact.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return SendBulkEmailResult.Success(bulkEmailSend.Id, sentCount, skippedCount, failedCount);
    }
}
