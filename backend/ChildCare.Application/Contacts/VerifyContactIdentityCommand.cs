using ChildCare.Domain.Enums;
using MediatR;

namespace ChildCare.Application.Contacts;

// spec.md FR-002/FR-004/FR-005/FR-006: records (or corrects) a contact's identity verification,
// independent of which children the contact is linked to. Same shape/reasoning as
// VerifyChildIdentityCommand.
public record VerifyContactIdentityCommand(
    Guid ContactId,
    IdDocumentType? DocumentType,
    string? Note,
    Guid VerifiedByUserId,
    string VerifiedByEmail) : IRequest<ContactResult>;
