using ChildCare.Application.Common;
using FluentValidation;
using MediatR;

namespace ChildCare.Application.Payments;

// Feature 014a — contracts/014a-invoice-payments-plus/payments-api.md, spec.md FR-001.
public record ConnectPaymentProviderCommand : IRequest<ConnectPaymentProviderResult>;

public record ConnectPaymentProviderResult(string AuthorizationUrl);

public class ConnectPaymentProviderCommandValidator : AbstractValidator<ConnectPaymentProviderCommand> { }

public class ConnectPaymentProviderCommandHandler(IPaymentProvider paymentProvider, ICurrentTenantService currentTenant)
    : IRequestHandler<ConnectPaymentProviderCommand, ConnectPaymentProviderResult>
{
    public Task<ConnectPaymentProviderResult> Handle(ConnectPaymentProviderCommand request, CancellationToken cancellationToken)
    {
        // The callback is itself DirectorOnly-authenticated (a valid JWT is required to
        // complete the connection), so the tenant id doubling as OAuth `state` is a reasonable
        // CSRF-bound-by-authentication posture rather than a separate token store.
        var authorizationUrl = paymentProvider.GetOAuthAuthorizationUrl(currentTenant.TenantId.ToString());
        return Task.FromResult(new ConnectPaymentProviderResult(authorizationUrl));
    }
}
