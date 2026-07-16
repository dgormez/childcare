using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Payments;

// Feature 014a — contracts/014a-invoice-payments-plus/payments-api.md, spec.md FR-003.
public record DisconnectPaymentProviderCommand : IRequest<DisconnectPaymentProviderResult>;

public record DisconnectPaymentProviderResult(bool Found);

public class DisconnectPaymentProviderCommandValidator : AbstractValidator<DisconnectPaymentProviderCommand> { }

public class DisconnectPaymentProviderCommandHandler(IPublicDbContext publicDb, ICurrentTenantService currentTenant)
    : IRequestHandler<DisconnectPaymentProviderCommand, DisconnectPaymentProviderResult>
{
    public async Task<DisconnectPaymentProviderResult> Handle(DisconnectPaymentProviderCommand request, CancellationToken cancellationToken)
    {
        var connection = await publicDb.PaymentProviderConnections
            .FirstOrDefaultAsync(c => c.TenantId == currentTenant.TenantId && c.Status == PaymentConnectionStatus.Connected, cancellationToken);
        if (connection is null)
            return new DisconnectPaymentProviderResult(false);

        connection.Status = PaymentConnectionStatus.Disconnected;
        connection.DisconnectedAt = DateTime.UtcNow;
        await publicDb.SaveChangesAsync(cancellationToken);

        return new DisconnectPaymentProviderResult(true);
    }
}
