using ChildCare.Domain.Entities;

namespace ChildCare.Application.Contracts;

/// <summary>
/// Derived signing status shown to a director (feature 024-esignature, FR-018, data-model.md) —
/// computed from Contract's existing fields, not stored as a separate column/enum, mirroring
/// this codebase's established preference for evolving fields over a parallel status table
/// (feature 022/023 precedent).
/// </summary>
public enum ContractSigningStatus
{
    NotSent,
    Pending,
    Expired,
    Signed,
}

public static class ContractSigningStatusResolver
{
    public static ContractSigningStatus Resolve(Contract contract, DateTime utcNow)
    {
        if (contract.SignedAt is not null)
            return ContractSigningStatus.Signed;

        if (contract.SigningToken is null)
            return ContractSigningStatus.NotSent;

        return contract.SigningTokenExpiresAt > utcNow
            ? ContractSigningStatus.Pending
            : ContractSigningStatus.Expired;
    }
}
