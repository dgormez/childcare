using ChildCare.Domain.Enums;
using MediatR;

namespace ChildCare.Application.Children;

// spec.md FR-001/FR-004/FR-005/FR-006: records (or corrects) a child's identity verification.
// VerifiedByUserId/VerifiedByEmail are resolved server-side from the caller's JWT claims by the
// endpoint, never accepted from the request body (mirrors DeactivateVaccineTypeCommand, 013h).
// DocumentType is nullable here — a missing/unparseable wire value and an explicitly-omitted one
// both flow into the same NotNull validator rule (errors.child.document_type_required, FR-003),
// rather than the endpoint risking an Enum.Parse throw on bad input.
public record VerifyChildIdentityCommand(
    Guid ChildId,
    IdDocumentType? DocumentType,
    string? Note,
    Guid VerifiedByUserId,
    string VerifiedByEmail) : IRequest<ChildResult>;
