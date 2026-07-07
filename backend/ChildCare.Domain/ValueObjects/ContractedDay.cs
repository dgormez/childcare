namespace ChildCare.Domain.ValueObjects;

// Owned JSONB element on Contract.ContractedDays (research.md R1) — Monday-Friday only,
// each weekday appearing at most once per contract, with its own independent hours.
public class ContractedDay
{
    public DayOfWeek Weekday  { get; set; }
    public TimeOnly  StartTime { get; set; }
    public TimeOnly  EndTime   { get; set; }
}
