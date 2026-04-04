namespace Opalop.Infrastructure.Redis;

using Opalop.Application.Interfaces;
using Opalop.Domain.ValueObjects;
using StackExchange.Redis;

public class RedisColorIndex : IColorIndex
{
    private readonly RedisConnectionManager _connectionManager;

    /// <summary>
    /// Lua script that atomically checks usage count and increments it if under the limit.
    /// KEYS[1] = usage hash key (job:{jobId}:usage)
    /// ARGV[1] = photoId string
    /// ARGV[2] = max usage per photo
    /// Returns 1 if usage was claimed (incremented), 0 if limit reached.
    /// </summary>
    private const string ClaimUsageLuaScript = @"
local current = tonumber(redis.call('HGET', KEYS[1], ARGV[1])) or 0
local maxUsage = tonumber(ARGV[2])
if current >= maxUsage then
    return 0
end
redis.call('HINCRBY', KEYS[1], ARGV[1], 1)
return 1
";

    public RedisColorIndex(RedisConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task AddPhotoAsync(Guid userId, Guid photoId, ColorFingerprint fingerprint, CancellationToken ct = default)
    {
        var db = _connectionManager.GetDatabase();
        var sortedSetKey = SortedSetKey(userId);
        var hashKey = PhotoHashKey(userId, photoId);
        var photoIdStr = photoId.ToString();

        var batch = db.CreateBatch();

        var addSortedSetTask = batch.SortedSetAddAsync(sortedSetKey, photoIdStr, fingerprint.Total.L);
        var setHashTask = batch.HashSetAsync(hashKey, FingerprintToHashEntries(fingerprint));

        batch.Execute();

        await addSortedSetTask;
        await setHashTask;
    }

    public async Task RemovePhotoAsync(Guid userId, Guid photoId, CancellationToken ct = default)
    {
        var db = _connectionManager.GetDatabase();
        var sortedSetKey = SortedSetKey(userId);
        var hashKey = PhotoHashKey(userId, photoId);
        var photoIdStr = photoId.ToString();

        var batch = db.CreateBatch();

        var removeSortedSetTask = batch.SortedSetRemoveAsync(sortedSetKey, photoIdStr);
        var deleteHashTask = batch.KeyDeleteAsync(hashKey);

        batch.Execute();

        await removeSortedSetTask;
        await deleteHashTask;
    }

    public async Task<ColorMatchResult?> FindBestMatchAsync(
        Guid userId, ColorFingerprint target, Guid jobId,
        int maxUsagePerPhoto = 5, CancellationToken ct = default)
    {
        var db = _connectionManager.GetDatabase();
        var sortedSetKey = SortedSetKey(userId);
        var usageKey = UsageKey(jobId);
        var targetL = target.Total.L;

        float[] tolerances = [10f, 25f, 50f];

        foreach (var tolerance in tolerances)
        {
            var minL = targetL - tolerance;
            var maxL = targetL + tolerance;

            // Get candidate photoIds from the sorted set by L* range
            var candidates = await db.SortedSetRangeByScoreAsync(sortedSetKey, minL, maxL);

            if (candidates.Length == 0)
                continue;

            // Collect all fingerprints for candidates in parallel
            var fingerprintTasks = new (string PhotoIdStr, Task<HashEntry[]> HashTask)[candidates.Length];
            for (int i = 0; i < candidates.Length; i++)
            {
                var photoIdStr = candidates[i].ToString();
                var hashKey = PhotoHashKey(userId, photoIdStr);
                fingerprintTasks[i] = (photoIdStr, db.HashGetAllAsync(hashKey));
            }

            // Evaluate candidates by weighted ΔE, pick best match
            float bestDeltaE = float.MaxValue;
            string? bestPhotoIdStr = null;
            ColorFingerprint bestFingerprint = default;

            foreach (var (photoIdStr, hashTask) in fingerprintTasks)
            {
                var entries = await hashTask;
                if (entries.Length == 0)
                    continue;

                var fp = HashEntriesToFingerprint(entries);
                var deltaE = target.WeightedDeltaE(fp);

                if (deltaE < bestDeltaE)
                {
                    bestDeltaE = deltaE;
                    bestPhotoIdStr = photoIdStr;
                    bestFingerprint = fp;
                }
            }

            if (bestPhotoIdStr is null)
                continue;

            // Try to claim usage atomically via Lua script
            var claimed = (int)await db.ScriptEvaluateAsync(
                ClaimUsageLuaScript,
                new RedisKey[] { usageKey },
                new RedisValue[] { bestPhotoIdStr, maxUsagePerPhoto });

            if (claimed == 1)
            {
                return new ColorMatchResult(
                    Guid.Parse(bestPhotoIdStr),
                    bestDeltaE,
                    bestFingerprint);
            }

            // If best match is exhausted, try remaining candidates in ΔE order
            var sortedCandidates = new List<(string PhotoIdStr, float DeltaE, ColorFingerprint Fp)>();
            foreach (var (photoIdStr, hashTask) in fingerprintTasks)
            {
                if (photoIdStr == bestPhotoIdStr)
                    continue;

                var entries = await hashTask;
                if (entries.Length == 0)
                    continue;

                var fp = HashEntriesToFingerprint(entries);
                var deltaE = target.WeightedDeltaE(fp);
                sortedCandidates.Add((photoIdStr, deltaE, fp));
            }

            sortedCandidates.Sort((a, b) => a.DeltaE.CompareTo(b.DeltaE));

            foreach (var (photoIdStr, deltaE, fp) in sortedCandidates)
            {
                var claimedAlt = (int)await db.ScriptEvaluateAsync(
                    ClaimUsageLuaScript,
                    new RedisKey[] { usageKey },
                    new RedisValue[] { photoIdStr, maxUsagePerPhoto });

                if (claimedAlt == 1)
                {
                    return new ColorMatchResult(
                        Guid.Parse(photoIdStr),
                        deltaE,
                        fp);
                }
            }

            // All candidates in this tolerance are exhausted, try wider tolerance
        }

        return null;
    }

    public async Task RebuildIndexAsync(
        Guid userId, IEnumerable<(Guid PhotoId, ColorFingerprint Fingerprint)> photos,
        CancellationToken ct = default)
    {
        var db = _connectionManager.GetDatabase();
        var sortedSetKey = SortedSetKey(userId);

        // Get existing photo IDs from sorted set to clean up their hashes
        var existingMembers = await db.SortedSetRangeByScoreAsync(sortedSetKey);

        var batch = db.CreateBatch();
        var deleteTasks = new List<Task>();

        // Delete existing sorted set
        deleteTasks.Add(batch.KeyDeleteAsync(sortedSetKey));

        // Delete all existing photo hashes
        foreach (var member in existingMembers)
        {
            var hashKey = PhotoHashKey(userId, member.ToString());
            deleteTasks.Add(batch.KeyDeleteAsync(hashKey));
        }

        batch.Execute();
        await Task.WhenAll(deleteTasks);

        // Add new photos
        foreach (var (photoId, fingerprint) in photos)
        {
            await AddPhotoAsync(userId, photoId, fingerprint, ct);
        }
    }

    private static string SortedSetKey(Guid userId) => $"user:{userId}:colors";
    private static string PhotoHashKey(Guid userId, Guid photoId) => $"user:{userId}:photo:{photoId}";
    private static string PhotoHashKey(Guid userId, string photoIdStr) => $"user:{userId}:photo:{photoIdStr}";
    private static string UsageKey(Guid jobId) => $"job:{jobId}:usage";

    private static HashEntry[] FingerprintToHashEntries(ColorFingerprint fp) =>
    [
        new("total_L", (double)fp.Total.L),
        new("total_a", (double)fp.Total.A),
        new("total_b", (double)fp.Total.B),
        new("q0_L", (double)fp.TopLeft.L),
        new("q0_a", (double)fp.TopLeft.A),
        new("q0_b", (double)fp.TopLeft.B),
        new("q1_L", (double)fp.TopRight.L),
        new("q1_a", (double)fp.TopRight.A),
        new("q1_b", (double)fp.TopRight.B),
        new("q2_L", (double)fp.BottomLeft.L),
        new("q2_a", (double)fp.BottomLeft.A),
        new("q2_b", (double)fp.BottomLeft.B),
        new("q3_L", (double)fp.BottomRight.L),
        new("q3_a", (double)fp.BottomRight.A),
        new("q3_b", (double)fp.BottomRight.B),
    ];

    private static ColorFingerprint HashEntriesToFingerprint(HashEntry[] entries)
    {
        var dict = new Dictionary<string, float>(entries.Length);
        foreach (var entry in entries)
        {
            dict[entry.Name.ToString()] = (float)(double)entry.Value;
        }

        return new ColorFingerprint(
            Total: new QuadrantLab(dict["total_L"], dict["total_a"], dict["total_b"]),
            TopLeft: new QuadrantLab(dict["q0_L"], dict["q0_a"], dict["q0_b"]),
            TopRight: new QuadrantLab(dict["q1_L"], dict["q1_a"], dict["q1_b"]),
            BottomLeft: new QuadrantLab(dict["q2_L"], dict["q2_a"], dict["q2_b"]),
            BottomRight: new QuadrantLab(dict["q3_L"], dict["q3_a"], dict["q3_b"]));
    }
}
