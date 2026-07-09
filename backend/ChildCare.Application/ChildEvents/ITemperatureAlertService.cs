namespace ChildCare.Application.ChildEvents;

/// <summary>
/// FR-010/FR-011/FR-011a/FR-011b: dispatches (or attempts to) a fever-alert push notification.
/// Never throws — every failure mode (no eligible recipients, transport error) is caught and
/// logged internally so a temperature event's save is never blocked or failed by this.
/// </summary>
public interface ITemperatureAlertService
{
    Task NotifyAsync(Guid childId, decimal celsius, CancellationToken cancellationToken = default);
}
