namespace ChildCare.Contracts.Requests;

// Feature 014a — contracts/014a-invoice-payments-plus/payments-api.md.
public record CompletePaymentConnectionRequest(string AuthorizationCode);
