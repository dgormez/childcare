using ChildCare.Domain.Enums;

namespace ChildCare.Domain.Entities;

// Feature 014a (data-model.md) — one row per organisation's connected Mollie sub-merchant
// account. Lives in the public schema (research.md R3): the public webhook must resolve and
// use these credentials before any tenant context is established.
public class PaymentProviderConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    // Plain string, not an enum — a second provider (research.md R1) never needs a migration
    // to be added.
    public string Provider { get; set; } = "mollie";

    public string ProviderAccountId { get; set; } = string.Empty;
    public string ProviderAccountLabel { get; set; } = string.Empty;

    // IDataProtector-encrypted (research.md R3). Never serialized to any API response.
    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string EncryptedRefreshToken { get; set; } = string.Empty;
    public DateTime TokenExpiresAt { get; set; }

    public PaymentConnectionStatus Status { get; set; } = PaymentConnectionStatus.Connected;

    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DisconnectedAt { get; set; }
}
