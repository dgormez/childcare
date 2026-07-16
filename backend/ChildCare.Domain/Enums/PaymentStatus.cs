namespace ChildCare.Domain.Enums;

// Feature 014a — mirrors Mollie's own payment-status vocabulary (research.md R6). Terminal
// states (Paid/Failed/Cancelled/Expired) never transition further; only an Open payment can be
// reused by a subsequent "Pay now" tap (research.md R6, 2026-07-16 clarification).
public enum PaymentStatus
{
    Open,
    Paid,
    Failed,
    Cancelled,
    Expired,
}
