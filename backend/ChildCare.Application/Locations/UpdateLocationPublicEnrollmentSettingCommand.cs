using MediatR;

namespace ChildCare.Application.Locations;

// Feature 023 — spec.md FR-001/FR-002/FR-012. A sibling setting to feature 021's
// UpdateLocationQrCheckInSettingCommand on the same Location entity, not a reuse of it — the two
// settings are unrelated (QR check-in entry point vs. public enrollment form availability).
public record UpdateLocationPublicEnrollmentSettingCommand(
    Guid LocationId,
    Guid DirectorId,
    bool Enabled) : IRequest<LocationResult>;
