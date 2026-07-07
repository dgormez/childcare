using ChildCare.Domain.Enums;
using ChildCare.Domain.ValueObjects;

namespace ChildCare.Domain.Entities;

public class Contract
{
    public Guid Id                 { get; set; } = Guid.NewGuid();
    public Guid ChildId            { get; set; }
    public Guid LocationId         { get; set; }

    // Set on the successor contract created by an amendment (research.md R5); null for a
    // fresh first-ever contract or one with no predecessor.
    public Guid? PreviousContractId { get; set; }

    public DateOnly  StartDate { get; set; }
    public DateOnly? EndDate   { get; set; }

    public List<ContractedDay> ContractedDays { get; set; } = [];

    public int DailyRateCents { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Draft;

    public ContractConsent Consent { get; set; } = new();

    // Reserved for Phase 3 IKT subsidy-rate support — always null in this feature (FR-013).
    public string?   TariefCode     { get; set; }
    public DateOnly? RateValidUntil { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
