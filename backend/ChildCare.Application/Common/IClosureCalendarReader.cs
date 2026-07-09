namespace ChildCare.Application.Common;

public interface IClosureCalendarReader
{
    Task<IReadOnlyList<DateOnly>> ListPublishedClosureDatesAsync(
        Guid locationId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
