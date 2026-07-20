using MediatR;

namespace ChildCare.Application.Locations;

// Feature 021 — spec.md FR-001/FR-002. A sibling setting to feature 008b's
// UpdateLocationCheckInSettingsCommand (RequiresCaregiverPin) on the same Location entity, not a
// reuse of it — the two settings are unrelated (PIN identity assurance vs. QR scan entry point).
public record UpdateLocationQrCheckInSettingCommand(
    Guid LocationId,
    Guid DirectorId,
    bool Enabled) : IRequest<LocationResult>;
