using ChildCare.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Contracts;

/// <summary>
/// Sends (or resends) a contract's signing invitation email (feature 024-esignature, User
/// Stories 1/3). A single command serves both operations — resending overwrites the previous
/// token exactly like a first send (FR-002/FR-004), so there is no separate resend command.
/// </summary>
public record SendContractSigningInvitationCommand(Guid Id) : IRequest<ContractResult>;

public class SendContractSigningInvitationCommandHandler(
    ITenantDbContext db,
    IPublicDbContext publicDb,
    ICurrentTenantService currentTenant,
    IContractSigningTokenService signingTokenService,
    IEmailSender emailSender,
    Microsoft.Extensions.Configuration.IConfiguration configuration)
    : IRequestHandler<SendContractSigningInvitationCommand, ContractResult>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(72);

    public async Task<ContractResult> Handle(SendContractSigningInvitationCommand request, CancellationToken cancellationToken)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (contract is null)
            return ContractResult.Fail(ContractFailure.NotFound);

        // FR-014: a signed contract's terms are frozen; this also prevents re-sending an
        // invitation for a contract that's already been signed.
        if (contract.SignedAt is not null)
            return ContractResult.Fail(ContractFailure.AlreadySigned);

        // FR-001: only a Draft contract can be sent for signature — see spec.md's Clarifications
        // on why "finalises" maps to Draft, not a separate status.
        if (contract.Status != Domain.Enums.ContractStatus.Draft)
            return ContractResult.Fail(ContractFailure.NotDraft);

        // FR-016: the organisation's SEPA Creditor Identifier must be configured before any
        // mandate can be captured against it.
        var tenant = await publicDb.Tenants.FirstAsync(t => t.Id == currentTenant.TenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(tenant.SepaCreditorIdentifier))
            return ContractResult.Fail(ContractFailure.CreditorIdNotConfigured);

        // FR-001, research.md R9: resolve the primary contact via the same IsPrimary-ordered
        // join GenerateInvoicePdfQuery already uses to find the billing-relevant contact.
        var primaryContact = await db.ChildContacts
            .Where(cc => cc.ChildId == contract.ChildId)
            .OrderByDescending(cc => cc.IsPrimary)
            .Join(db.Contacts, cc => cc.ContactId, c => c.Id, (cc, c) => c)
            .FirstOrDefaultAsync(cancellationToken);

        if (primaryContact?.Email is null)
            return ContractResult.Fail(ContractFailure.NoContactEmail);

        var child = await db.Children.FirstAsync(c => c.Id == contract.ChildId, cancellationToken);
        var location = await db.Locations.FirstAsync(l => l.Id == contract.LocationId, cancellationToken);

        // FR-002/FR-004: overwrite any previously issued, unsigned token — a first send and a
        // resend are the same operation.
        var token = signingTokenService.CreateToken(contract.Id);
        contract.SigningToken = token;
        contract.SigningTokenExpiresAt = DateTime.UtcNow.Add(TokenLifetime);
        contract.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var signingUrl = ContractSigningLinkBuilder.BuildSigningUrl(configuration, token, currentTenant.TenantSlug);
        var childName = $"{child.FirstName} {child.LastName}".Trim();
        var locale = string.IsNullOrWhiteSpace(primaryContact.Locale) ? "nl" : primaryContact.Locale;

        await emailSender.SendContractSigningInvitationAsync(
            primaryContact.Email, locale, childName, location.Name, signingUrl, cancellationToken);

        return ContractResult.Success(ContractMapper.ToResponse(contract));
    }
}
