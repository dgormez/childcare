namespace ChildCare.Contracts.Requests;

public record CreateLocationRequest(
    string Name,
    string Address,
    string Phone,
    string Email,
    int MaxCapacity);

public record UpdateLocationRequest(
    string Name,
    string Address,
    string Phone,
    string Email,
    int MaxCapacity,
    string? NaamLocatie,
    string? Dossiernummer,
    string? Verantwoordelijke,
    bool FlexPermission,
    bool BoPermission);

public record UpdateLocationReservationSettingsRequest(
    string AbsencesMode,
    string ExtrasMode,
    string SwapsMode,
    int NoticeHours,
    bool ConfirmDespitePending);

public record UpdateLocationCheckInSettingsRequest(bool RequiresCaregiverPin);

// Feature 013j — contracts/013j-monthly-menu-variants/monthly-menu-variants-api.md.
// ConfirmDespiteRemovingPublished mirrors 013f's ConfirmDespitePending shape (FR-014).
public record UpdateLocationMenuVariantSettingsRequest(
    IReadOnlyList<string> MenuVariantPriorityOrder,
    bool ConfirmDespiteRemovingPublished);

// Feature 014 — contracts/014-invoicing/invoicing-api.md.
public record UpdateLocationInvoiceSettingsRequest(
    string? Erkenningsnummer,
    string? BankAccountNumber,
    int InvoiceDueDays);

// Feature 014a — contracts/014a-invoice-payments-plus/payments-api.md.
public record UpdateLocationPaymentReminderSettingsRequest(
    bool Enabled,
    int DelayDays,
    int CadenceDays);

// Feature 030 — contracts/family-siblings-api.md.
public record UpdateLocationSiblingBillingSettingsRequest(
    decimal SiblingDiscountPct,
    bool FamilyInvoiceBundlingEnabled);

// Feature 021 — contracts/021-qr-checkin/qr-checkin-api.md.
public record UpdateLocationQrCheckInSettingRequest(bool Enabled);
