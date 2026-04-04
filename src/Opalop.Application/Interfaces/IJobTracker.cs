namespace Opalop.Application.Interfaces;

public interface IJobTracker
{
    Task InitJobAsync(Guid jobId, int totalTiles, Guid userId, int pxFormat, CancellationToken ct = default);
    Task<int> IncrementCompletedAsync(Guid jobId, CancellationToken ct = default);
    Task<JobInfo?> GetJobInfoAsync(Guid jobId, CancellationToken ct = default);
    Task SetJobCompletedAsync(Guid jobId, string resultPath, CancellationToken ct = default);
    Task SetJobFailedAsync(Guid jobId, string error, CancellationToken ct = default);
    Task<bool> TryAcquireLockAsync(Guid userId, TimeSpan ttl, CancellationToken ct = default);
    Task ReleaseLockAsync(Guid userId, CancellationToken ct = default);
}

public record JobInfo(Guid JobId, int TotalTiles, int CompletedTiles, Guid UserId, int PxFormat, string Status);
