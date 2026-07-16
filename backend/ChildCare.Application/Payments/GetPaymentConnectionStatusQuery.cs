using ChildCare.Application.Common;
using ChildCare.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ChildCare.Application.Payments;

// Feature 014a — contracts/014a-invoice-payments-plus/payments-api.md, spec.md FR-002. Never
// returns EncryptedAccessToken/EncryptedRefreshToken — only a connected/disconnected status and
// the provider's own account label.
public record GetPaymentConnectionStatusQuery : IRequest<GetPaymentConnectionStatusResult>;

public record GetPaymentConnectionStatusResult(string Status, string? ProviderAccountLabel, DateTime? ConnectedAt);

public class GetPaymentConnectionStatusQueryHandler(IPublicDbContext publicDb, ICurrentTenantService currentTenant)
    : IRequestHandler<GetPaymentConnectionStatusQuery, GetPaymentConnectionStatusResult>
{
    public async Task<GetPaymentConnectionStatusResult> Handle(GetPaymentConnectionStatusQuery request, CancellationToken cancellationToken)
    {
        var connection = await publicDb.PaymentProviderConnections
            .FirstOrDefaultAsync(c => c.TenantId == currentTenant.TenantId && c.Status == PaymentConnectionStatus.Connected, cancellationToken);

        return connection is null
            ? new GetPaymentConnectionStatusResult("disconnected", null, null)
            : new GetPaymentConnectionStatusResult("connected", connection.ProviderAccountLabel, connection.ConnectedAt);
    }
}
