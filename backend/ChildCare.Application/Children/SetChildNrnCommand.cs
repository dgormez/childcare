using MediatR;

namespace ChildCare.Application.Children;

// spec.md FR-009/FR-010: sets/updates a child's National Register Number. Nrn is the raw,
// as-entered value (with or without dot/dash separators) — normalization and validation happen
// in the validator/handler, never at this boundary.
public record SetChildNrnCommand(Guid ChildId, string Nrn) : IRequest<ChildResult>;
