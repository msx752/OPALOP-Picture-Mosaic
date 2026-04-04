namespace Opalop.Infrastructure.Redis;

using System.Text.Json;
using Opalop.Application.Interfaces;
using Opalop.Domain.ValueObjects;
using StackExchange.Redis;

public class RedisMosaicQueue : IMosaicQueue
{
    private const string StreamKey = "mosaic:tiles";
    private const string GroupName = "workers";
    private static readonly TimeSpan DefaultBlockTimeout = TimeSpan.FromMilliseconds(1000);

    private readonly RedisConnectionManager _connectionManager;
    private bool _groupCreated;

    public RedisMosaicQueue(RedisConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task EnqueueTilesAsync(Guid jobId, IReadOnlyList<TileTask> tiles, CancellationToken ct = default)
    {
        var db = _connectionManager.GetDatabase();
        await EnsureConsumerGroupAsync(db);

        foreach (var tile in tiles)
        {
            var entries = new NameValueEntry[]
            {
                new("jobId", jobId.ToString()),
                new("tileIndex", tile.TileIndex),
                new("x", tile.X),
                new("y", tile.Y),
                new("fingerprint", JsonSerializer.Serialize(tile.Fingerprint)),
            };

            await db.StreamAddAsync(StreamKey, entries);
        }
    }

    public async Task<TileTask?> DequeueAsync(string consumerName, CancellationToken ct = default)
    {
        var db = _connectionManager.GetDatabase();
        await EnsureConsumerGroupAsync(db);

        var results = await db.StreamReadGroupAsync(
            StreamKey,
            GroupName,
            consumerName,
            position: ">",
            count: 1,
            noAck: false);

        if (results is null || results.Length == 0)
            return null;

        var entry = results[0];
        var fields = entry.Values.ToDictionary(
            v => v.Name.ToString(),
            v => v.Value.ToString());

        var fingerprint = JsonSerializer.Deserialize<ColorFingerprint>(fields["fingerprint"]);

        return new TileTask(
            MessageId: entry.Id.ToString(),
            JobId: Guid.Parse(fields["jobId"]),
            TileIndex: int.Parse(fields["tileIndex"]),
            X: int.Parse(fields["x"]),
            Y: int.Parse(fields["y"]),
            Fingerprint: fingerprint);
    }

    public async Task AcknowledgeAsync(string messageId, CancellationToken ct = default)
    {
        var db = _connectionManager.GetDatabase();
        await db.StreamAcknowledgeAsync(StreamKey, GroupName, messageId);
    }

    public async Task ClaimStaleMessagesAsync(string consumerName, TimeSpan idleTimeout, CancellationToken ct = default)
    {
        var db = _connectionManager.GetDatabase();
        await EnsureConsumerGroupAsync(db);

        // Get pending messages that have been idle longer than the timeout
        var pending = await db.StreamPendingMessagesAsync(
            StreamKey,
            GroupName,
            count: 100,
            consumerName: RedisValue.Null,
            minId: null,
            maxId: null);

        if (pending is null || pending.Length == 0)
            return;

        var staleIds = pending
            .Where(p => p.IdleTimeInMilliseconds >= (long)idleTimeout.TotalMilliseconds)
            .Select(p => p.MessageId)
            .ToArray();

        if (staleIds.Length == 0)
            return;

        await db.StreamClaimAsync(
            StreamKey,
            GroupName,
            consumerName,
            (long)idleTimeout.TotalMilliseconds,
            staleIds);
    }

    private async Task EnsureConsumerGroupAsync(IDatabase db)
    {
        if (_groupCreated)
            return;

        try
        {
            await db.StreamCreateConsumerGroupAsync(StreamKey, GroupName, "0-0", createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Consumer group already exists, which is fine
        }

        _groupCreated = true;
    }
}
