namespace Opalop.Infrastructure.Redis;

using Opalop.Application.Interfaces;
using Opalop.Domain.ValueObjects;
using StackExchange.Redis;

public class RedisColorIndex : IColorIndex
{
    private readonly RedisConnectionManager _connectionManager;

    /// <summary>
    /// Lua script that atomically checks usage count, spatial distance, and increments if allowed.
    /// KEYS[1] = usage count hash (job:{jobId}:usage)
    /// KEYS[2] = spatial positions hash (job:{jobId}:positions:{photoId})
    /// ARGV[1] = photoId string
    /// ARGV[2] = max usage per photo
    /// ARGV[3] = current tile row
    /// ARGV[4] = current tile col
    /// ARGV[5] = minimum Manhattan distance
    /// Returns 1 if usage was claimed, 0 if rejected (usage limit or too close).
    /// </summary>
    private const string ClaimUsageLuaScript = @"
local current = tonumber(redis.call('HGET', KEYS[1], ARGV[1])) or 0
local maxUsage = tonumber(ARGV[2])
if current >= maxUsage then
    return 0
end
local tileRow = tonumber(ARGV[3])
local tileCol = tonumber(ARGV[4])
local minDist = tonumber(ARGV[5])
if minDist > 0 and tileRow >= 0 then
    local positions = redis.call('LRANGE', KEYS[2], 0, -1)
    for i = 1, #positions, 2 do
        local pr = tonumber(positions[i])
        local pc = tonumber(positions[i+1])
        local dist = math.abs(tileRow - pr) + math.abs(tileCol - pc)
        if dist < minDist then
            return 0
        end
    end
end
redis.call('HINCRBY', KEYS[1], ARGV[1], 1)
if tileRow >= 0 then
    redis.call('RPUSH', KEYS[2], tileRow, tileCol)
    redis.call('EXPIRE', KEYS[2], 3600)
end
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
        int maxUsagePerPhoto = 5, int tileRow = -1, int tileCol = -1, int minDistance = 3,
        CancellationToken ct = default)
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

            // Two-pass ranking: CIE76 coarse filter → CIEDE2000 precise ranking
            var allCandidates = new List<(string PhotoIdStr, float DeltaE76, ColorFingerprint Fp)>();

            foreach (var (photoIdStr, hashTask) in fingerprintTasks)
            {
                var entries = await hashTask;
                if (entries.Length == 0)
                    continue;

                var fp = HashEntriesToFingerprint(entries);
                var deltaE76 = target.WeightedDeltaE(fp);
                allCandidates.Add((photoIdStr, deltaE76, fp));
            }

            // Rank top candidates by CIEDE2000 for perceptually accurate final selection
            var topN = allCandidates.OrderBy(c => c.DeltaE76).Take(10).ToList();
            float bestDeltaE = float.MaxValue;
            string? bestPhotoIdStr = null;
            ColorFingerprint bestFingerprint = default;

            foreach (var (photoIdStr, _, fp) in topN)
            {
                var deltaE2000 = target.WeightedDeltaE2000(fp);
                if (deltaE2000 < bestDeltaE)
                {
                    bestDeltaE = deltaE2000;
                    bestPhotoIdStr = photoIdStr;
                    bestFingerprint = fp;
                }
            }

            if (bestPhotoIdStr is null)
                continue;

            // Try to claim usage atomically via Lua script (with spatial guard)
            var positionsKey = PositionsKey(jobId, bestPhotoIdStr);
            var claimed = (int)await db.ScriptEvaluateAsync(
                ClaimUsageLuaScript,
                new RedisKey[] { usageKey, positionsKey },
                new RedisValue[] { bestPhotoIdStr, maxUsagePerPhoto, tileRow, tileCol, minDistance });

            if (claimed == 1)
            {
                return new ColorMatchResult(
                    Guid.Parse(bestPhotoIdStr),
                    bestDeltaE,
                    bestFingerprint);
            }

            // If best match is exhausted, try remaining candidates ranked by CIEDE2000
            var sortedCandidates = allCandidates
                .Where(c => c.PhotoIdStr != bestPhotoIdStr)
                .Select(c => (c.PhotoIdStr, DeltaE: target.WeightedDeltaE2000(c.Fp), c.Fp))
                .OrderBy(c => c.DeltaE)
                .ToList();

            foreach (var (photoIdStr, deltaE, fp) in sortedCandidates)
            {
                var altPositionsKey = PositionsKey(jobId, photoIdStr);
                var claimedAlt = (int)await db.ScriptEvaluateAsync(
                    ClaimUsageLuaScript,
                    new RedisKey[] { usageKey, altPositionsKey },
                    new RedisValue[] { photoIdStr, maxUsagePerPhoto, tileRow, tileCol, minDistance });

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
    private static string PositionsKey(Guid jobId, string photoIdStr) => $"job:{jobId}:pos:{photoIdStr}";

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
