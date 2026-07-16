using ChildCare.Application.Common;
using ChildCare.Domain.Entities;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Payments;

// Feature 014a — contracts/014a-invoice-payments-plus/payments-api.md, spec.md FR-001/FR-002.
// Reconnecting after a disconnect updates the SAME row back to Connected rather than creating a
// duplicate (spec.md Edge Cases — the "reconnect" case).
public record CompletePaymentProviderOAuthCommand(string AuthorizationCode) : IRequest<CompletePaymentProviderOAuthResult>;

public class CompletePaymentProviderOAuthResult
{
    public bool Succeeded { get; private init; }
    public string? ProviderAccountLabel { get; private init; }

    public static CompletePaymentProviderOAuthResult Success(string label) => new() { Succeeded = true, ProviderAccountLabel = label };
    public static CompletePaymentProviderOAuthResult Fail() => new() { Succeeded = false };
}

public class CompletePaymentProviderOAuthCommandValidator : AbstractValidator<CompletePaymentProviderOAuthCommand>
{
    public CompletePaymentProviderOAuthCommandValidator()
    {
        RuleFor(x => x.AuthorizationCode).NotEmpty();
    }
}

public class CompletePaymentProviderOAuthCommandHandler(
    IPublicDbContext publicDb, ICurrentTenantService currentTenant, IPaymentProvider paymentProvider, IPaymentTokenProtector tokenProtector)
    : IRequestHandler<CompletePaymentProviderOAuthCommand, CompletePaymentProviderOAuthResult>
{
    public async Task<CompletePaymentProviderOAuthResult> Handle(CompletePaymentProviderOAuthCommand request, CancellationToken cancellationToken)
    {
        var oauthResult = await paymentProvider.CompleteOAuthConnectionAsync(request.AuthorizationCode, cancellationToken);
        if (!oauthResult.Succeeded)
            return CompletePaymentProviderOAuthResult.Fail();

        var connection = await publicDb.PaymentProviderConnections
            .FirstOrDefaultAsync(c => c.TenantId == currentTenant.TenantId, cancellationToken);

        if (connection is null)
        {
            connection = new PaymentProviderConnection { TenantId = currentTenant.TenantId };
            publicDb.PaymentProviderConnections.Add(connection);
        }

        connection.ProviderAccountId = oauthResult.ProviderAccountId!;
        connection.ProviderAccountLabel = oauthResult.ProviderAccountLabel!;
        connection.EncryptedAccessToken = tokenProtector.Protect(oauthResult.AccessToken!);
        connection.EncryptedRefreshToken = tokenProtector.Protect(oauthResult.RefreshToken!);
        connection.TokenExpiresAt = oauthResult.ExpiresAt!.Value;
        connection.Status = PaymentConnectionStatus.Connected;
        connection.ConnectedAt = DateTime.UtcNow;
        connection.DisconnectedAt = null;

        await publicDb.SaveChangesAsync(cancellationToken);

        return CompletePaymentProviderOAuthResult.Success(connection.ProviderAccountLabel);
    }
}
