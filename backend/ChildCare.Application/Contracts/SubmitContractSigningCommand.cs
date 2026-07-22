using ChildCare.Application.Auth;
using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChildCare.Application.Contracts;

/// <summary>
/// Public, tenant-exempt (FR-005/FR-021). Records the signature + SEPA mandate, generates and
/// persists the final signed PDF, and emails both parties (User Story 1, FR-009/FR-010/FR-011).
/// </summary>
public record SubmitContractSigningCommand(
    string OrganisationSlug,
    string Token,
    string SignatureType,
    string SignatureData,
    string SepaIban,
    string SignedByIp) : IRequest<SubmitContractSigningResult>;

public class SubmitContractSigningResult
{
    public bool Succeeded { get; private init; }

    public static SubmitContractSigningResult Success() => new() { Succeeded = true };
    public static SubmitContractSigningResult Fail() => new() { Succeeded = false };
}

public class SubmitContractSigningCommandValidator : AbstractValidator<SubmitContractSigningCommand>
{
    public SubmitContractSigningCommandValidator()
    {
        RuleFor(x => x.SignatureType)
            .Must(t => t is "Drawn" or "Typed").WithMessage("errors.contract_signing.invalid_signature_type");
        RuleFor(x => x.SignatureData).NotEmpty().WithMessage("errors.contract_signing.signature_required");
        // FR-008: format + mod-97 checksum, not Belgium-only (spec.md Edge Cases).
        RuleFor(x => x.SepaIban)
            .Must(iban => IbanValidation.IsValid(iban)).WithMessage("errors.contract_signing.invalid_iban");
    }
}

public class SubmitContractSigningCommandHandler(
    OrganisationSlugResolver slugResolver,
    ITenantDbContextResolver tenantResolver,
    IContractSigningTokenService signingTokenService,
    IIbanProtector ibanProtector,
    IContractPdfGenerator pdfGenerator,
    ISignedContractStorage signedContractStorage,
    IEmailSender emailSender,
    ILogger<SubmitContractSigningCommandHandler> logger)
    : IRequestHandler<SubmitContractSigningCommand, SubmitContractSigningResult>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(72);

    public async Task<SubmitContractSigningResult> Handle(SubmitContractSigningCommand request, CancellationToken cancellationToken)
    {
        var tenant = await slugResolver.ResolveAsync(request.OrganisationSlug, cancellationToken);
        if (tenant is null)
            return SubmitContractSigningResult.Fail();

        var contractId = signingTokenService.TryParseToken(request.Token);
        if (contractId is null)
            return SubmitContractSigningResult.Fail();

        var db = tenantResolver.ForSchema(tenant.SchemaName);

        var signedAt = DateTime.UtcNow;
        var signatureType = Enum.Parse<Domain.Enums.SignatureType>(request.SignatureType);
        var mandateReference = SepaMandateReferenceGenerator.Generate();
        var ibanNormalized = request.SepaIban.Replace(" ", string.Empty).ToUpperInvariant();
        var ibanEncrypted = ibanProtector.Protect(ibanNormalized);
        var ibanLast4 = ibanNormalized[^4..];

        // FR-009: the check-and-invalidate step must be atomic against a concurrent second
        // submission of the same token (research.md R2's Concurrency note, spec.md Edge Cases) —
        // a single conditional UPDATE, not a read-then-write sequence. Only a row that still
        // matches this exact token AND has never been signed can be affected.
        var affected = await db.Contracts
            .Where(c => c.Id == contractId
                     && c.SigningToken == request.Token
                     && c.SignedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(c => c.SignedAt, signedAt)
                .SetProperty(c => c.SignatureData, request.SignatureData)
                .SetProperty(c => c.SignatureType, signatureType)
                .SetProperty(c => c.SignedByIp, request.SignedByIp)
                .SetProperty(c => c.SepaIbanEncrypted, ibanEncrypted)
                .SetProperty(c => c.SepaIbanLast4, ibanLast4)
                .SetProperty(c => c.SepaMandateReference, mandateReference)
                .SetProperty(c => c.SepaAuthorisedAt, signedAt)
                .SetProperty(c => c.SigningToken, (string?)null)
                .SetProperty(c => c.SigningTokenExpiresAt, (DateTime?)null)
                .SetProperty(c => c.UpdatedAt, signedAt),
                cancellationToken);

        if (affected == 0)
            return SubmitContractSigningResult.Fail();

        var contract = await db.Contracts.FirstAsync(c => c.Id == contractId, cancellationToken);
        var child = await db.Children.FirstAsync(c => c.Id == contract.ChildId, cancellationToken);
        var location = await db.Locations.FirstAsync(l => l.Id == contract.LocationId, cancellationToken);

        var primaryContact = await db.ChildContacts
            .Where(cc => cc.ChildId == contract.ChildId)
            .OrderByDescending(cc => cc.IsPrimary)
            .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => c)
            .FirstOrDefaultAsync(cancellationToken);
        var locale = string.IsNullOrWhiteSpace(primaryContact?.Locale) ? "nl" : primaryContact!.Locale;

        var pdfModel = new ContractPdfModel(
            $"{child.FirstName} {child.LastName}",
            location.Name,
            contract.Status.ToString().ToLowerInvariant(),
            contract.ContractedDays.Select(d => new ContractPdfDay(d.Weekday, d.StartTime, d.EndTime)).ToList(),
            contract.DailyRateCents,
            contract.Consent.PhotosInternal,
            contract.Consent.PhotosWebsite,
            contract.Consent.PhotosSocialMedia,
            contract.Consent.VideoInternal,
            contract.Consent.PhotosPress,
            locale,
            new ContractPdfSignature(
                signedAt,
                request.SignatureType,
                request.SignatureData,
                request.SignedByIp,
                mandateReference,
                $"•••• {ibanLast4}",
                tenant.SepaCreditorIdentifier ?? string.Empty,
                signedAt));

        byte[] pdfBytes;
        try
        {
            pdfBytes = await pdfGenerator.GenerateAsync(pdfModel, cancellationToken);
            await signedContractStorage.UploadAsync(contract.Id, pdfBytes, cancellationToken);
        }
        catch (Exception ex)
        {
            // The atomic UPDATE above already consumed the token — if the PDF never actually
            // made it to storage, that's not a completed signature (FR-010's persisted PDF is
            // the whole point), so restore the contract to its pre-signed, still-invitable state
            // rather than stranding the parent with a burned token and no way to retry the exact
            // same link. Safe only because nothing downstream (email) has happened yet.
            logger.LogError(ex, "Failed to generate/store signed PDF for contract {ContractId} — restoring signing token", contractId);
            await db.Contracts
                .Where(c => c.Id == contractId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(c => c.SignedAt, (DateTime?)null)
                    .SetProperty(c => c.SignatureData, (string?)null)
                    .SetProperty(c => c.SignatureType, (Domain.Enums.SignatureType?)null)
                    .SetProperty(c => c.SignedByIp, (string?)null)
                    .SetProperty(c => c.SepaIbanEncrypted, (string?)null)
                    .SetProperty(c => c.SepaIbanLast4, (string?)null)
                    .SetProperty(c => c.SepaMandateReference, (string?)null)
                    .SetProperty(c => c.SepaAuthorisedAt, (DateTime?)null)
                    .SetProperty(c => c.SigningToken, request.Token)
                    .SetProperty(c => c.SigningTokenExpiresAt, DateTime.UtcNow.Add(TokenLifetime)),
                    CancellationToken.None);
            throw;
        }

        // FR-006 (staff invitation) precedent: the signature/mandate/PDF are already durably
        // recorded at this point, so a notification failure must not undo or fail the response —
        // logged, recoverable via a manual resend, exactly like CreateStaffProfileCommandHandler.
        if (primaryContact?.Email is not null)
        {
            try
            {
                await emailSender.SendSignedContractAsync(primaryContact.Email, locale, pdfModel.ChildName, pdfBytes, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to email signed contract to parent {Email} for contract {ContractId}", primaryContact.Email, contractId);
            }
        }

        var directorEmails = await db.Users
            .Where(u => u.Role == UserRole.Director)
            .Select(u => u.Email)
            .ToListAsync(cancellationToken);
        foreach (var directorEmail in directorEmails)
        {
            try
            {
                await emailSender.SendSignedContractAsync(directorEmail, "nl", pdfModel.ChildName, pdfBytes, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to email signed contract to director {Email} for contract {ContractId}", directorEmail, contractId);
            }
        }

        return SubmitContractSigningResult.Success();
    }
}
