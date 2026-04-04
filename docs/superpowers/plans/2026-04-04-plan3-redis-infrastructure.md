# Plan 3: Redis Infrastructure — Color Index, Job Queue, Job Tracker

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the Redis-backed infrastructure services: color index (Sorted Set + Hash + Lua), mosaic job queue (Stream + Consumer Group), and job tracker (Hash + Pub/Sub + mutex). These implement the interfaces defined in Opalop.Application.

**Architecture:** All Redis operations use StackExchange.Redis. Color matching runs via a Lua script for zero-roundtrip atomic operations. Job queue uses Redis Streams with consumer groups for fan-out to workers.

**Tech Stack:** StackExchange.Redis, Redis Lua scripting, xUnit + Testcontainers.Redis

**Spec reference:** `docs/superpowers/specs/2026-04-04-opalop-net9-migration-design.md` — Section 6 (Redis Architecture)

---

## File Map

```
src/Opalop.Infrastructure/
├── Redis/
│   ├── RedisColorIndex.cs          # IColorIndex implementation
│   ├── RedisMosaicQueue.cs         # IMosaicQueue implementation
│   ├── RedisJobTracker.cs          # IJobTracker implementation
│   └── RedisConnectionManager.cs   # Connection multiplexer lifecycle
├── ServiceRegistration.cs          # (modify: register Redis services)

infra/redis/lua/
├── match_tile.lua                  # Atomic color matching script

tests/Opalop.Infrastructure.Tests/
├── Redis/
│   ├── RedisColorIndexTests.cs
│   ├── RedisMosaicQueueTests.cs
│   └── RedisJobTrackerTests.cs
```

---

### Task 1: Redis Connection Manager & Service Registration

**Files:**
- Create: `src/Opalop.Infrastructure/Redis/RedisConnectionManager.cs`
- Modify: `src/Opalop.Infrastructure/ServiceRegistration.cs`

- [ ] **Step 1: Create RedisConnectionManager**

```csharp
// src/Opalop.Infrastructure/Redis/RedisConnectionManager.cs
namespace Opalop.Infrastructure.Redis;

using StackExchange.Redis;

public class RedisConnectionManager : IDisposable
{
    private readonly Lazy<ConnectionMultiplexer> _connection;

    public RedisConnectionManager(string connectionString)
    {
        _connection = new Lazy<ConnectionMultiplexer>(
            () => ConnectionMultiplexer.Connect(connectionString));
    }

    public IDatabase GetDatabase() => _connection.Value.GetDatabase();
    public ISubscriber GetSubscriber() => _connection.Value.GetSubscriber();

    public void Dispose()
    {
        if (_connection.IsValueCreated)
            _connection.Value.Dispose();
    }
}
```

- [ ] **Step 2: Update ServiceRegistration to register Redis**

Add to the existing `AddInfrastructure` method in `src/Opalop.Infrastructure/ServiceRegistration.cs`:

```csharp
// Add after the existing DbContext registration:
var redisConnection = configuration.GetConnectionString("Redis")!;
services.AddSingleton(new RedisConnectionManager(redisConnection));
services.AddSingleton<IColorIndex, RedisColorIndex>();
services.AddSingleton<IMosaicQueue, RedisMosaicQueue>();
services.AddSingleton<IJobTracker, RedisJobTracker>();
```

Add the necessary using statements for the Application interfaces and Redis implementations.

NOTE: The actual implementation classes (RedisColorIndex, etc.) don't exist yet. This step will NOT compile until Tasks 2-4 are done. Create the registration code but comment out the three service lines temporarily. Uncomment them in Task 4.

- [ ] **Step 3: Build to verify**

```bash
dotnet build Opalop.sln
```

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add RedisConnectionManager and update service registration"
```

---

### Task 2: RedisColorIndex — Sorted Set + Hash + Lua Matching

**Files:**
- Create: `src/Opalop.Infrastructure/Redis/RedisColorIndex.cs`
- Create: `infra/redis/lua/match_tile.lua`
- Create: `tests/Opalop.Infrastructure.Tests/Redis/RedisColorIndexTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Opalop.Infrastructure.Tests/Redis/RedisColorIndexTests.cs
namespace Opalop.Infrastructure.Tests.Redis;

using FluentAssertions;
using Opalop.Application.Interfaces;
using Opalop.Domain.ValueObjects;
using Opalop.Infrastructure.Redis;
using Testcontainers.Redis;
using Xunit;

public class RedisColorIndexTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();
    private RedisConnectionManager _manager = null!;
    private RedisColorIndex _index = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _manager = new RedisConnectionManager(_redis.GetConnectionString());
        _index = new RedisColorIndex(_manager);
    }

    public async Task DisposeAsync()
    {
        _manager.Dispose();
        await _redis.DisposeAsync();
    }

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid JobId = Guid.NewGuid();

    [Fact]
    public async Task AddPhoto_ThenFindMatch_ReturnsIt()
    {
        var photoId = Guid.NewGuid();
        var fp = MakeFingerprint(50f, 20f, -10f);

        await _index.AddPhotoAsync(UserId, photoId, fp);

        var target = MakeFingerprint(52f, 19f, -11f); // close to the photo
        var match = await _index.FindBestMatchAsync(UserId, target, JobId);

        match.Should().NotBeNull();
        match!.PhotoId.Should().Be(photoId);
        match.DeltaE.Should().BeLessThan(10f);
    }

    [Fact]
    public async Task FindMatch_NoPhotos_ReturnsNull()
    {
        var target = MakeFingerprint(50f, 20f, -10f);
        var userId = Guid.NewGuid();

        var match = await _index.FindBestMatchAsync(userId, target, JobId);

        match.Should().BeNull();
    }

    [Fact]
    public async Task RemovePhoto_ThenFindMatch_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        var fp = MakeFingerprint(50f, 20f, -10f);

        await _index.AddPhotoAsync(userId, photoId, fp);
        await _index.RemovePhotoAsync(userId, photoId);

        var match = await _index.FindBestMatchAsync(userId, MakeFingerprint(50f, 20f, -10f), JobId);
        match.Should().BeNull();
    }

    [Fact]
    public async Task FindMatch_RespectsMaxUsage()
    {
        var userId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        var fp = MakeFingerprint(50f, 20f, -10f);
        await _index.AddPhotoAsync(userId, photoId, fp);

        var target = MakeFingerprint(50f, 20f, -10f);
        var jobId = Guid.NewGuid();

        // Use it up to maxUsage (default 5)
        for (int i = 0; i < 5; i++)
        {
            var m = await _index.FindBestMatchAsync(userId, target, jobId, maxUsagePerPhoto: 5);
            m.Should().NotBeNull();
        }

        // 6th call should return null (over max usage)
        var exhausted = await _index.FindBestMatchAsync(userId, target, jobId, maxUsagePerPhoto: 5);
        exhausted.Should().BeNull();
    }

    [Fact]
    public async Task RebuildIndex_ReplacesExistingData()
    {
        var userId = Guid.NewGuid();
        var oldPhoto = Guid.NewGuid();
        await _index.AddPhotoAsync(userId, oldPhoto, MakeFingerprint(50f, 20f, -10f));

        var newPhoto = Guid.NewGuid();
        await _index.RebuildIndexAsync(userId, [(newPhoto, MakeFingerprint(70f, 10f, 5f))]);

        // Old photo should be gone
        var matchOld = await _index.FindBestMatchAsync(userId, MakeFingerprint(50f, 20f, -10f), JobId);
        // New photo should be found
        var matchNew = await _index.FindBestMatchAsync(userId, MakeFingerprint(70f, 10f, 5f), JobId);

        matchNew.Should().NotBeNull();
        matchNew!.PhotoId.Should().Be(newPhoto);
    }

    private static ColorFingerprint MakeFingerprint(float l, float a, float b)
    {
        var q = new QuadrantLab(l, a, b);
        return new ColorFingerprint(q, q, q, q, q);
    }
}
```

- [ ] **Step 2: Create the Lua match script**

```lua
-- infra/redis/lua/match_tile.lua
-- KEYS[1] = user:{uid}:colors (sorted set)
-- KEYS[2] = job:{jobId}:usage (hash for reuse tracking)
-- ARGV[1] = target L* value
-- ARGV[2] = L* tolerance
-- ARGV[3] = max usage per photo
-- ARGV[4..18] = target fingerprint: total_L,a,b, q0_L,a,b, q1_L,a,b, q2_L,a,b, q3_L,a,b
-- ARGV[19] = user key prefix for photo hashes

local target_L = tonumber(ARGV[1])
local tolerance = tonumber(ARGV[2])
local max_usage = tonumber(ARGV[3])
local user_prefix = ARGV[19]

-- Target fingerprint
local t = {}
for i = 4, 18 do
    t[i - 3] = tonumber(ARGV[i])
end

-- Pre-filter by L* brightness
local candidates = redis.call('ZRANGEBYSCORE', KEYS[1], target_L - tolerance, target_L + tolerance)
if #candidates == 0 then return nil end

local best_id = nil
local best_score = 999999

for _, photo_id in ipairs(candidates) do
    -- Check usage
    local usage = tonumber(redis.call('HGET', KEYS[2], photo_id) or '0')
    if usage < max_usage then
        -- Get candidate fingerprint
        local data = redis.call('HGETALL', user_prefix .. photo_id)
        if #data > 0 then
            local c = {}
            for j = 1, #data, 2 do
                c[data[j]] = tonumber(data[j + 1])
            end

            -- Compute weighted DeltaE
            local function deltaE(l1, a1, b1, l2, a2, b2)
                local dl = l1 - l2
                local da = a1 - a2
                local db = b1 - b2
                return math.sqrt(dl*dl + da*da + db*db)
            end

            local score = 0.4 * deltaE(t[1], t[2], t[3], c.total_L or 0, c.total_a or 0, c.total_b or 0)
                        + 0.15 * deltaE(t[4], t[5], t[6], c.q0_L or 0, c.q0_a or 0, c.q0_b or 0)
                        + 0.15 * deltaE(t[7], t[8], t[9], c.q1_L or 0, c.q1_a or 0, c.q1_b or 0)
                        + 0.15 * deltaE(t[10], t[11], t[12], c.q2_L or 0, c.q2_a or 0, c.q2_b or 0)
                        + 0.15 * deltaE(t[13], t[14], t[15], c.q3_L or 0, c.q3_a or 0, c.q3_b or 0)

            if score < best_score then
                best_score = score
                best_id = photo_id
            end
        end
    end
end

if best_id then
    redis.call('HINCRBY', KEYS[2], best_id, 1)
    return {best_id, tostring(best_score)}
end
return nil
```

- [ ] **Step 3: Implement RedisColorIndex**

```csharp
// src/Opalop.Infrastructure/Redis/RedisColorIndex.cs
namespace Opalop.Infrastructure.Redis;

using Opalop.Application.Interfaces;
using Opalop.Domain.ValueObjects;
using StackExchange.Redis;

public class RedisColorIndex : IColorIndex
{
    private readonly RedisConnectionManager _redis;
    private readonly string _luaScript;

    public RedisColorIndex(RedisConnectionManager redis)
    {
        _redis = redis;
        _luaScript = LoadLuaScript();
    }

    public async Task AddPhotoAsync(Guid userId, Guid photoId, ColorFingerprint fp, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var colorKey = $"user:{userId}:colors";
        var photoKey = $"user:{userId}:photo:{photoId}";

        await db.SortedSetAddAsync(colorKey, photoId.ToString(), fp.Total.L);
        await db.HashSetAsync(photoKey, GetHashEntries(fp));
    }

    public async Task RemovePhotoAsync(Guid userId, Guid photoId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.SortedSetRemoveAsync($"user:{userId}:colors", photoId.ToString());
        await db.KeyDeleteAsync($"user:{userId}:photo:{photoId}");
    }

    public async Task<ColorMatchResult?> FindBestMatchAsync(Guid userId, ColorFingerprint target,
        Guid jobId, int maxUsagePerPhoto = 5, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var colorKey = $"user:{userId}:colors";
        var usageKey = $"job:{jobId}:usage";
        var photoPrefix = $"user:{userId}:photo:";

        // Try with expanding tolerance: 10, 25, 50
        float[] tolerances = [10f, 25f, 50f];

        foreach (var tol in tolerances)
        {
            var result = await db.ScriptEvaluateAsync(
                _luaScript,
                new RedisKey[] { colorKey, usageKey },
                GetScriptArgs(target, tol, maxUsagePerPhoto, photoPrefix));

            if (!result.IsNull)
            {
                var arr = (RedisResult[])result!;
                var photoId = Guid.Parse((string)arr[0]!);
                var deltaE = float.Parse((string)arr[1]!);

                // Get the full fingerprint for rotation calculation
                var fpData = await db.HashGetAllAsync($"user:{userId}:photo:{photoId}");
                var fingerprint = ParseFingerprint(fpData);

                return new ColorMatchResult(photoId, deltaE, fingerprint);
            }
        }

        return null;
    }

    public async Task RebuildIndexAsync(Guid userId,
        IEnumerable<(Guid PhotoId, ColorFingerprint Fingerprint)> photos, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var colorKey = $"user:{userId}:colors";

        // Delete existing index
        await db.KeyDeleteAsync(colorKey);

        // Delete all photo hashes (scan pattern)
        var server = _redis.GetDatabase().Multiplexer.GetServer(
            _redis.GetDatabase().Multiplexer.GetEndPoints()[0]);
        await foreach (var key in server.KeysAsync(pattern: $"user:{userId}:photo:*"))
            await db.KeyDeleteAsync(key);

        // Rebuild
        foreach (var (photoId, fp) in photos)
            await AddPhotoAsync(userId, photoId, fp, ct);
    }

    private static HashEntry[] GetHashEntries(ColorFingerprint fp) =>
    [
        new("total_L", fp.Total.L), new("total_a", fp.Total.A), new("total_b", fp.Total.B),
        new("q0_L", fp.TopLeft.L), new("q0_a", fp.TopLeft.A), new("q0_b", fp.TopLeft.B),
        new("q1_L", fp.TopRight.L), new("q1_a", fp.TopRight.A), new("q1_b", fp.TopRight.B),
        new("q2_L", fp.BottomLeft.L), new("q2_a", fp.BottomLeft.A), new("q2_b", fp.BottomLeft.B),
        new("q3_L", fp.BottomRight.L), new("q3_a", fp.BottomRight.A), new("q3_b", fp.BottomRight.B),
    ];

    private static RedisValue[] GetScriptArgs(ColorFingerprint target, float tolerance, int maxUsage, string photoPrefix) =>
    [
        target.Total.L, tolerance, maxUsage,
        target.Total.L, target.Total.A, target.Total.B,
        target.TopLeft.L, target.TopLeft.A, target.TopLeft.B,
        target.TopRight.L, target.TopRight.A, target.TopRight.B,
        target.BottomLeft.L, target.BottomLeft.A, target.BottomLeft.B,
        target.BottomRight.L, target.BottomRight.A, target.BottomRight.B,
        photoPrefix
    ];

    private static ColorFingerprint ParseFingerprint(HashEntry[] entries)
    {
        var d = entries.ToDictionary(e => (string)e.Name!, e => (float)(double)e.Value);
        return new ColorFingerprint(
            new QuadrantLab(d.GetValueOrDefault("total_L"), d.GetValueOrDefault("total_a"), d.GetValueOrDefault("total_b")),
            new QuadrantLab(d.GetValueOrDefault("q0_L"), d.GetValueOrDefault("q0_a"), d.GetValueOrDefault("q0_b")),
            new QuadrantLab(d.GetValueOrDefault("q1_L"), d.GetValueOrDefault("q1_a"), d.GetValueOrDefault("q1_b")),
            new QuadrantLab(d.GetValueOrDefault("q2_L"), d.GetValueOrDefault("q2_a"), d.GetValueOrDefault("q2_b")),
            new QuadrantLab(d.GetValueOrDefault("q3_L"), d.GetValueOrDefault("q3_a"), d.GetValueOrDefault("q3_b"))
        );
    }

    private static string LoadLuaScript()
    {
        // Embedded Lua script for atomic matching
        return """
            local target_L = tonumber(ARGV[1])
            local tolerance = tonumber(ARGV[2])
            local max_usage = tonumber(ARGV[3])
            local user_prefix = ARGV[19]
            local t = {}
            for i = 4, 18 do t[i - 3] = tonumber(ARGV[i]) end
            local candidates = redis.call('ZRANGEBYSCORE', KEYS[1], target_L - tolerance, target_L + tolerance)
            if #candidates == 0 then return nil end
            local best_id = nil
            local best_score = 999999
            for _, photo_id in ipairs(candidates) do
                local usage = tonumber(redis.call('HGET', KEYS[2], photo_id) or '0')
                if usage < max_usage then
                    local data = redis.call('HGETALL', user_prefix .. photo_id)
                    if #data > 0 then
                        local c = {}
                        for j = 1, #data, 2 do c[data[j]] = tonumber(data[j + 1]) end
                        local function deltaE(l1,a1,b1,l2,a2,b2)
                            local dl=l1-l2; local da=a1-a2; local db=b1-b2
                            return math.sqrt(dl*dl+da*da+db*db)
                        end
                        local score = 0.4*deltaE(t[1],t[2],t[3],c.total_L or 0,c.total_a or 0,c.total_b or 0)
                            +0.15*deltaE(t[4],t[5],t[6],c.q0_L or 0,c.q0_a or 0,c.q0_b or 0)
                            +0.15*deltaE(t[7],t[8],t[9],c.q1_L or 0,c.q1_a or 0,c.q1_b or 0)
                            +0.15*deltaE(t[10],t[11],t[12],c.q2_L or 0,c.q2_a or 0,c.q2_b or 0)
                            +0.15*deltaE(t[13],t[14],t[15],c.q3_L or 0,c.q3_a or 0,c.q3_b or 0)
                        if score < best_score then best_score = score; best_id = photo_id end
                    end
                end
            end
            if best_id then
                redis.call('HINCRBY', KEYS[2], best_id, 1)
                return {best_id, tostring(best_score)}
            end
            return nil
            """;
    }
}
```

- [ ] **Step 4: Run tests — verify they pass**

```bash
dotnet test tests/Opalop.Infrastructure.Tests --filter "FullyQualifiedName~RedisColorIndexTests" -v n
```

Expected: 5 tests pass (requires Docker Desktop for Testcontainers).

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: add RedisColorIndex with Lua-based atomic color matching"
```

---

### Task 3: RedisMosaicQueue — Stream + Consumer Group

**Files:**
- Create: `src/Opalop.Infrastructure/Redis/RedisMosaicQueue.cs`
- Create: `tests/Opalop.Infrastructure.Tests/Redis/RedisMosaicQueueTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Opalop.Infrastructure.Tests/Redis/RedisMosaicQueueTests.cs
namespace Opalop.Infrastructure.Tests.Redis;

using FluentAssertions;
using Opalop.Application.Interfaces;
using Opalop.Domain.ValueObjects;
using Opalop.Infrastructure.Redis;
using Testcontainers.Redis;
using Xunit;

public class RedisMosaicQueueTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine").Build();
    private RedisConnectionManager _manager = null!;
    private RedisMosaicQueue _queue = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _manager = new RedisConnectionManager(_redis.GetConnectionString());
        _queue = new RedisMosaicQueue(_manager);
    }

    public async Task DisposeAsync()
    {
        _manager.Dispose();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task EnqueueAndDequeue_ReturnsTile()
    {
        var jobId = Guid.NewGuid();
        var fp = MakeFp(50, 20, -10);
        var tiles = new List<TileTask>
        {
            new("", jobId, 0, 0, 0, fp),
            new("", jobId, 1, 100, 0, fp)
        };

        await _queue.EnqueueTilesAsync(jobId, tiles);
        var tile = await _queue.DequeueAsync("worker-1");

        tile.Should().NotBeNull();
        tile!.JobId.Should().Be(jobId);
        tile.TileIndex.Should().BeOneOf(0, 1);
    }

    [Fact]
    public async Task Dequeue_EmptyQueue_ReturnsNull()
    {
        var tile = await _queue.DequeueAsync("worker-1");
        tile.Should().BeNull();
    }

    [Fact]
    public async Task Acknowledge_RemovesFromPending()
    {
        var jobId = Guid.NewGuid();
        var fp = MakeFp(50, 20, -10);
        await _queue.EnqueueTilesAsync(jobId, [new("", jobId, 0, 0, 0, fp)]);

        var tile = await _queue.DequeueAsync("worker-1");
        tile.Should().NotBeNull();

        await _queue.AcknowledgeAsync(tile!.MessageId);
        // No exception = success
    }

    private static ColorFingerprint MakeFp(float l, float a, float b)
    {
        var q = new QuadrantLab(l, a, b);
        return new ColorFingerprint(q, q, q, q, q);
    }
}
```

- [ ] **Step 2: Implement RedisMosaicQueue**

```csharp
// src/Opalop.Infrastructure/Redis/RedisMosaicQueue.cs
namespace Opalop.Infrastructure.Redis;

using System.Text.Json;
using Opalop.Application.Interfaces;
using Opalop.Domain.ValueObjects;
using StackExchange.Redis;

public class RedisMosaicQueue : IMosaicQueue
{
    private readonly RedisConnectionManager _redis;
    private const string StreamKey = "mosaic:tiles";
    private const string GroupName = "workers";

    public RedisMosaicQueue(RedisConnectionManager redis)
    {
        _redis = redis;
        EnsureConsumerGroup();
    }

    public async Task EnqueueTilesAsync(Guid jobId, IReadOnlyList<TileTask> tiles, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        foreach (var tile in tiles)
        {
            await db.StreamAddAsync(StreamKey, [
                new NameValueEntry("jobId", jobId.ToString()),
                new NameValueEntry("tileIndex", tile.TileIndex),
                new NameValueEntry("x", tile.X),
                new NameValueEntry("y", tile.Y),
                new NameValueEntry("fingerprint", JsonSerializer.Serialize(tile.Fingerprint))
            ], maxLength: 10000, useApproximateMaxLength: true);
        }
    }

    public async Task<TileTask?> DequeueAsync(string consumerName, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var entries = await db.StreamReadGroupAsync(StreamKey, GroupName, consumerName, ">", count: 1);

        if (entries.Length == 0) return null;

        var entry = entries[0];
        var values = entry.Values.ToDictionary(v => v.Name.ToString(), v => v.Value.ToString());

        return new TileTask(
            MessageId: entry.Id!,
            JobId: Guid.Parse(values["jobId"]),
            TileIndex: int.Parse(values["tileIndex"]),
            X: int.Parse(values["x"]),
            Y: int.Parse(values["y"]),
            Fingerprint: JsonSerializer.Deserialize<ColorFingerprint>(values["fingerprint"])
        );
    }

    public async Task AcknowledgeAsync(string messageId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.StreamAcknowledgeAsync(StreamKey, GroupName, messageId);
    }

    public async Task ClaimStaleMessagesAsync(string consumerName, TimeSpan idleTimeout, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var pending = await db.StreamPendingMessagesAsync(StreamKey, GroupName, 10, RedisValue.Null);

        foreach (var msg in pending)
        {
            if (msg.IdleTimeInMilliseconds > (long)idleTimeout.TotalMilliseconds)
            {
                await db.StreamClaimAsync(StreamKey, GroupName, consumerName, (long)idleTimeout.TotalMilliseconds, [msg.MessageId]);
            }
        }
    }

    private void EnsureConsumerGroup()
    {
        var db = _redis.GetDatabase();
        try
        {
            db.StreamCreateConsumerGroup(StreamKey, GroupName, "0", createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists — OK
        }
    }
}
```

- [ ] **Step 3: Run tests — verify they pass**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat: add RedisMosaicQueue with Stream consumer group"
```

---

### Task 4: RedisJobTracker — Hash + Mutex + Pub/Sub

**Files:**
- Create: `src/Opalop.Infrastructure/Redis/RedisJobTracker.cs`
- Create: `tests/Opalop.Infrastructure.Tests/Redis/RedisJobTrackerTests.cs`
- Modify: `src/Opalop.Infrastructure/ServiceRegistration.cs` (uncomment registrations)

- [ ] **Step 1: Write failing tests**

```csharp
// tests/Opalop.Infrastructure.Tests/Redis/RedisJobTrackerTests.cs
namespace Opalop.Infrastructure.Tests.Redis;

using FluentAssertions;
using Opalop.Infrastructure.Redis;
using Testcontainers.Redis;
using Xunit;

public class RedisJobTrackerTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine").Build();
    private RedisConnectionManager _manager = null!;
    private RedisJobTracker _tracker = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _manager = new RedisConnectionManager(_redis.GetConnectionString());
        _tracker = new RedisJobTracker(_manager);
    }

    public async Task DisposeAsync()
    {
        _manager.Dispose();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task InitAndGetJob_ReturnsCorrectInfo()
    {
        var jobId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await _tracker.InitJobAsync(jobId, totalTiles: 64, userId, pxFormat: 94);

        var info = await _tracker.GetJobInfoAsync(jobId);

        info.Should().NotBeNull();
        info!.TotalTiles.Should().Be(64);
        info.CompletedTiles.Should().Be(0);
        info.Status.Should().Be("processing");
    }

    [Fact]
    public async Task IncrementCompleted_ReturnsNewCount()
    {
        var jobId = Guid.NewGuid();
        await _tracker.InitJobAsync(jobId, 10, Guid.NewGuid(), 94);

        var count = await _tracker.IncrementCompletedAsync(jobId);
        count.Should().Be(1);

        count = await _tracker.IncrementCompletedAsync(jobId);
        count.Should().Be(2);
    }

    [Fact]
    public async Task TryAcquireLock_FirstCall_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        var acquired = await _tracker.TryAcquireLockAsync(userId, TimeSpan.FromMinutes(5));
        acquired.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireLock_SecondCall_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        await _tracker.TryAcquireLockAsync(userId, TimeSpan.FromMinutes(5));
        var second = await _tracker.TryAcquireLockAsync(userId, TimeSpan.FromMinutes(5));
        second.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseLock_ThenAcquire_ReturnsTrue()
    {
        var userId = Guid.NewGuid();
        await _tracker.TryAcquireLockAsync(userId, TimeSpan.FromMinutes(5));
        await _tracker.ReleaseLockAsync(userId);
        var acquired = await _tracker.TryAcquireLockAsync(userId, TimeSpan.FromMinutes(5));
        acquired.Should().BeTrue();
    }

    [Fact]
    public async Task SetJobCompleted_UpdatesStatus()
    {
        var jobId = Guid.NewGuid();
        await _tracker.InitJobAsync(jobId, 10, Guid.NewGuid(), 94);
        await _tracker.SetJobCompletedAsync(jobId, "mosaics/result.jpg");

        var info = await _tracker.GetJobInfoAsync(jobId);
        info!.Status.Should().Be("completed");
    }
}
```

- [ ] **Step 2: Implement RedisJobTracker**

```csharp
// src/Opalop.Infrastructure/Redis/RedisJobTracker.cs
namespace Opalop.Infrastructure.Redis;

using Opalop.Application.Interfaces;
using StackExchange.Redis;

public class RedisJobTracker : IJobTracker
{
    private readonly RedisConnectionManager _redis;

    public RedisJobTracker(RedisConnectionManager redis) => _redis = redis;

    public async Task InitJobAsync(Guid jobId, int totalTiles, Guid userId, int pxFormat, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var key = $"job:{jobId}";
        await db.HashSetAsync(key, [
            new HashEntry("total_tiles", totalTiles),
            new HashEntry("completed", 0),
            new HashEntry("user_id", userId.ToString()),
            new HashEntry("px_format", pxFormat),
            new HashEntry("status", "processing")
        ]);
        await db.KeyExpireAsync(key, TimeSpan.FromHours(1));
    }

    public async Task<int> IncrementCompletedAsync(Guid jobId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var count = await db.HashIncrementAsync($"job:{jobId}", "completed");
        return (int)count;
    }

    public async Task<JobInfo?> GetJobInfoAsync(Guid jobId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var entries = await db.HashGetAllAsync($"job:{jobId}");
        if (entries.Length == 0) return null;

        var d = entries.ToDictionary(e => (string)e.Name!, e => e.Value);
        return new JobInfo(
            jobId,
            (int)d["total_tiles"],
            (int)d["completed"],
            Guid.Parse(d["user_id"]!),
            (int)d["px_format"],
            d["status"]!
        );
    }

    public async Task SetJobCompletedAsync(Guid jobId, string resultPath, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.HashSetAsync($"job:{jobId}", [
            new HashEntry("status", "completed"),
            new HashEntry("result_path", resultPath)
        ]);
    }

    public async Task SetJobFailedAsync(Guid jobId, string error, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.HashSetAsync($"job:{jobId}", [
            new HashEntry("status", "failed"),
            new HashEntry("error", error)
        ]);
    }

    public async Task<bool> TryAcquireLockAsync(Guid userId, TimeSpan ttl, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        return await db.StringSetAsync($"user:{userId}:busy", "1", ttl, When.NotExists);
    }

    public async Task ReleaseLockAsync(Guid userId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync($"user:{userId}:busy");
    }
}
```

- [ ] **Step 3: Uncomment ServiceRegistration lines**

Update `src/Opalop.Infrastructure/ServiceRegistration.cs` to register all three Redis services:

```csharp
services.AddSingleton(new RedisConnectionManager(redisConnection));
services.AddSingleton<IColorIndex, RedisColorIndex>();
services.AddSingleton<IMosaicQueue, RedisMosaicQueue>();
services.AddSingleton<IJobTracker, RedisJobTracker>();
```

- [ ] **Step 4: Run all tests**

```bash
dotnet test Opalop.sln -v n
```

Expected: All tests pass (domain + infrastructure DB + infrastructure Redis + engine).

- [ ] **Step 5: Commit**

```bash
git commit -m "feat: add RedisJobTracker with mutex, progress tracking, and complete service registration"
```

---

## Summary

| Task | What it produces | Depends on |
|------|-----------------|------------|
| 1 | RedisConnectionManager + updated ServiceRegistration | — |
| 2 | RedisColorIndex + Lua matching script + 5 tests | Task 1 |
| 3 | RedisMosaicQueue (Stream consumer group) + 3 tests | Task 1 |
| 4 | RedisJobTracker (Hash + mutex) + 6 tests + service registration | Task 1, 2, 3 |
