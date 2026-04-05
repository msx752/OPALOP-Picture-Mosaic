namespace Opalop.Infrastructure.Redis;

using Opalop.Application.Interfaces;
using StackExchange.Redis;

public class RedisJobTracker : IJobTracker
{
    private readonly RedisConnectionManager _connectionManager;

    public RedisJobTracker(RedisConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task InitJobAsync(Guid jobId, int totalTiles, Guid userId, int pxFormat, byte opacity, int style, Guid? collectionId = null, CancellationToken ct = default)
    {
        var db = _connectionManager.GetDatabase();
        var key = JobKey(jobId);

        var entries = new List<HashEntry>
        {
            new("job_id", jobId.ToString()),
            new("total_tiles", totalTiles),
            new("completed", 0),
            new("user_id", userId.ToString()),
            new("px_format", pxFormat),
            new("opacity", (int)opacity),
            new("style", style),
            new("status", "processing"),
        };
        if (collectionId.HasValue)
            entries.Add(new("collection_id", collectionId.Value.ToString()));

        await db.HashSetAsync(key, entries.ToArray());
        await db.KeyExpireAsync(key, TimeSpan.FromHours(1));
    }

    public async Task<int> IncrementCompletedAsync(Guid jobId, CancellationToken ct = default)
    {
        var db = _connectionManager.GetDatabase();
        var newValue = await db.HashIncrementAsync(JobKey(jobId), "completed");
        return (int)newValue;
    }

    public async Task<JobInfo?> GetJobInfoAsync(Guid jobId, CancellationToken ct = default)
    {
        var db = _connectionManager.GetDatabase();
        var entries = await db.HashGetAllAsync(JobKey(jobId));

        if (entries.Length == 0)
            return null;

        var dict = new Dictionary<string, string>(entries.Length);
        foreach (var entry in entries)
        {
            dict[entry.Name.ToString()] = entry.Value.ToString();
        }

        return new JobInfo(
            JobId: Guid.Parse(dict["job_id"]),
            TotalTiles: int.Parse(dict["total_tiles"]),
            CompletedTiles: int.Parse(dict["completed"]),
            UserId: Guid.Parse(dict["user_id"]),
            PxFormat: int.Parse(dict["px_format"]),
            Opacity: dict.TryGetValue("opacity", out var opStr) ? byte.Parse(opStr) : (byte)128,
            Style: dict.TryGetValue("style", out var stStr) ? int.Parse(stStr) : 0,
            CollectionId: dict.TryGetValue("collection_id", out var colStr) ? Guid.Parse(colStr) : null,
            Status: dict["status"]);
    }

    public async Task SetJobCompletedAsync(Guid jobId, string resultPath, CancellationToken ct = default)
    {
        var db = _connectionManager.GetDatabase();
        var key = JobKey(jobId);

        await db.HashSetAsync(key, new HashEntry[]
        {
            new("status", "completed"),
            new("result_path", resultPath),
        });
    }

    public async Task SetJobFailedAsync(Guid jobId, string error, CancellationToken ct = default)
    {
        var db = _connectionManager.GetDatabase();
        var key = JobKey(jobId);

        await db.HashSetAsync(key, new HashEntry[]
        {
            new("status", "failed"),
            new("error", error),
        });
    }

    public async Task<bool> TryAcquireLockAsync(Guid userId, TimeSpan ttl, CancellationToken ct = default)
    {
        var db = _connectionManager.GetDatabase();
        var key = LockKey(userId);

        return await db.StringSetAsync(key, "1", ttl, When.NotExists);
    }

    public async Task ReleaseLockAsync(Guid userId, CancellationToken ct = default)
    {
        var db = _connectionManager.GetDatabase();
        await db.KeyDeleteAsync(LockKey(userId));
    }

    private static string JobKey(Guid jobId) => $"job:{jobId}";
    private static string LockKey(Guid userId) => $"user:{userId}:busy";
}
