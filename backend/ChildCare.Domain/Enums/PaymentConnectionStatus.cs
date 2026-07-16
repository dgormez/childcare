namespace ChildCare.Domain.Enums;

// Feature 014a — disconnecting sets Disconnected rather than deleting the row, so historical
// Payment rows stay attributable to the connection that created them (data-model.md).
public enum PaymentConnectionStatus
{
    Connected,
    Disconnected,
}
