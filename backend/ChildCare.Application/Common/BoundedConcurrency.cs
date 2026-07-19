namespace ChildCare.Application.Common;

/// <summary>
/// Runs an async action across a set of items with bounded parallelism (feature 020, FR-015 —
/// "batch...large sends rather than sending synchronously within a single request or job
/// invocation", so a 100+-recipient bulk email or daily digest doesn't block one HTTP
/// request/job tick for the full serial SMTP round-trip duration). A new job-queue dependency
/// was already rejected for this project (research.md R2, following 014a's own reasoning) —
/// bounded in-process parallelism satisfies the requirement without one. Each item's outcome is
/// independent of the others, so one item's exception never aborts the batch; callers still
/// handle their own try/catch per item for per-item failure detail.
/// </summary>
public static class BoundedConcurrency
{
    public const int DefaultMaxDegreeOfParallelism = 10;

    public static async Task ForEachAsync<T>(
        IEnumerable<T> items, Func<T, Task> action,
        int maxDegreeOfParallelism = DefaultMaxDegreeOfParallelism, CancellationToken cancellationToken = default)
    {
        using var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
        var tasks = items.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await action(item);
            }
            finally
            {
                semaphore.Release();
            }
        });
        await Task.WhenAll(tasks);
    }
}
