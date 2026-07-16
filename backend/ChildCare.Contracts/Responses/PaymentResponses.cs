namespace ChildCare.Contracts.Responses;

// Feature 014a — contracts/014a-invoice-payments-plus/payments-api.md.
public record PaymentConnectionResponse(
    string Status, // "connected" | "disconnected"
    string? ProviderAccountLabel,
    DateTime? ConnectedAt);

public record PaymentAuthorizationUrlResponse(string AuthorizationUrl);

public record PaymentLinkResponse(string CheckoutUrl);

public record PaymentStatusResponse(
    string InvoiceStatus, // "sent" | "paid"
    string? PaymentStatus); // "open" | "paid" | "failed" | "cancelled" | "expired" | null
