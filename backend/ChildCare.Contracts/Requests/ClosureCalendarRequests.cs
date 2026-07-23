namespace ChildCare.Contracts.Requests;

public record CreateClosureDayRequest(
    Guid LocationId,
    DateOnly Date,
    string Label,
    string ClosureType,
    bool NotifyParents);

public record UpdateClosureDayRequest(
    string Label,
    string ClosureType,
    bool NotifyParents);

public record PublishClosureDayRequest(bool ConfirmExistingAttendance, bool NotifyParents);
