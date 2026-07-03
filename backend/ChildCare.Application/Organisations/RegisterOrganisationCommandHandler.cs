using System.Data.Common;
using ChildCare.Application.Common;
using ChildCare.Application.Invitations;
using ChildCare.Contracts.Responses;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Organisations;

public class RegisterOrganisationCommandHandler(
    IPublicDbContext db,
    ITenantProvisioningService provisioning,
    IAccessTokenIssuer tokenIssuer) : IRequestHandler<RegisterOrganisationCommand, RegisterOrganisationResult>
{
    public async Task<RegisterOrganisationResult> Handle(RegisterOrganisationCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = InvitationTokenCodec.HashFromPlaintext(request.InvitationToken);

        // FR-003, FR-005: not-found and expired both fail the same way — the same generic
        // outcome regardless of *why* the token doesn't resolve (research.md R5).
        var invitation = tokenHash is null
            ? null
            : await db.Invitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

        if (invitation is null || invitation.ExpiresAt <= DateTime.UtcNow)
            return RegisterOrganisationResult.Fail(RegisterOrganisationFailure.InvitationNotFound);

        // FR-018: only checked once we know the caller holds a real invitation — a specific
        // "wrong email" message here doesn't leak anything about other tokens (research.md R5).
        var submittedEmail = request.Email.Trim().ToLowerInvariant();
        if (!string.Equals(invitation.Email, submittedEmail, StringComparison.Ordinal))
            return RegisterOrganisationResult.Fail(RegisterOrganisationFailure.EmailMismatch);

        var tenant = await ClaimOrResumeTenantAsync(invitation, request.OrganisationName, cancellationToken);
        if (tenant is null)
            // FR-004: another attempt already completed registration for this invitation.
            return RegisterOrganisationResult.Fail(RegisterOrganisationFailure.InvitationNotFound);

        var candidateDirectorUserId = Guid.NewGuid();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // FR-008: synchronous — this await does not return until provisioning has fully
        // succeeded or thrown. The returned Id is authoritative — under a genuine concurrent
        // race it may not be the candidate Id this call generated (research.md R15).
        Guid directorUserId;
        try
        {
            directorUserId = await provisioning.ProvisionAsync(
                tenant.SchemaName,
                candidateDirectorUserId,
                invitation.Email,
                passwordHash,
                request.DirectorName,
                cancellationToken);
        }
        catch
        {
            // Mark the failure explicitly rather than leaving `tenant` indistinguishable from
            // "still in progress" — a retry with the same invitation still resumes and succeeds
            // (ProvisionAsync is idempotent, FR-014); this only makes the stuck state observable.
            tenant.ProvisioningStatus = ProvisioningStatus.Failed;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }

        tenant.ProvisioningStatus = ProvisioningStatus.Ready;
        await db.SaveChangesAsync(cancellationToken);

        var accessToken = tokenIssuer.IssueAccessToken(directorUserId, invitation.Email, tenant.Id);

        return RegisterOrganisationResult.Success(new RegisterOrganisationResponse(
            accessToken,
            new OrganisationSummary(tenant.Id, tenant.Name, tenant.Slug, tenant.Plan.ToString().ToLowerInvariant()),
            new DirectorSummary(directorUserId, invitation.Email, request.DirectorName)));
    }

    /// <summary>
    /// Atomically claims this invitation for a new Tenant, or — if another attempt already
    /// claimed it — either resumes an incomplete attempt (FR-014) or reports it as already used
    /// (FR-004, returns null). The UNIQUE constraint on CreatedFromInvitationId is the sole
    /// arbiter of "who won" a concurrent race, regardless of how many app instances are running
    /// (research.md R10, FR-015). A slug/schema-name collision with a *different* invitation's
    /// tenant is handled separately, by retrying once with a suffixed slug (research.md R14).
    /// </summary>
    private async Task<Tenant?> ClaimOrResumeTenantAsync(Invitation invitation, string organisationName, CancellationToken ct)
    {
        var existing = await db.Tenants.FirstOrDefaultAsync(t => t.CreatedFromInvitationId == invitation.Id, ct);
        if (existing is not null)
            return existing.ProvisioningStatus == ProvisioningStatus.Ready ? null : existing;

        var baseSlug = OrganisationSlugGenerator.FromName(organisationName);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var candidateSlug = attempt == 0 ? baseSlug : OrganisationSlugGenerator.WithCollisionSuffix(baseSlug);
            var tenant = new Tenant
            {
                Name = organisationName,
                Slug = candidateSlug,
                SchemaName = $"tenant_{candidateSlug.Replace('-', '_')}",
                CreatedFromInvitationId = invitation.Id,
            };

            db.Tenants.Add(tenant);

            try
            {
                await db.SaveChangesAsync(ct);
                return tenant;
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                db.Detach(tenant);

                var raceWinner = await db.Tenants.FirstOrDefaultAsync(t => t.CreatedFromInvitationId == invitation.Id, ct);
                if (raceWinner is not null)
                    return raceWinner.ProvisioningStatus == ProvisioningStatus.Ready ? null : raceWinner;

                // Not an invitation race — a slug/schema-name collision with someone else's
                // tenant. Loop around and retry once with a suffixed slug.
            }
        }

        throw new InvalidOperationException("Failed to provision a unique organisation slug after retrying.");
    }

    // Only a genuine unique-constraint violation means "lost a race" — any other DbUpdateException
    // (a too-long OrganisationName hitting its column limit, a transient connection failure, etc.)
    // must NOT be silently retried and misclassified as a slug/invitation collision. SqlState is
    // the ANSI SQL-standard error code (23505 = unique_violation), available on any ADO.NET
    // provider's DbException — checked this way so Application stays provider-agnostic (no direct
    // Npgsql reference).
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        => ex.InnerException is DbException { SqlState: "23505" };
}
