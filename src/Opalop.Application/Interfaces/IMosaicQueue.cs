namespace Opalop.Application.Interfaces;

using Opalop.Domain.ValueObjects;

public interface IMosaicQueue
{
    Task EnqueueTilesAsync(Guid jobId, IReadOnlyList<TileTask> tiles, CancellationToken ct = default);
    Task<TileTask?> DequeueAsync(string consumerName, CancellationToken ct = default);
    Task AcknowledgeAsync(string messageId, CancellationToken ct = default);
    Task ClaimStaleMessagesAsync(string consumerName, TimeSpan idleTimeout, CancellationToken ct = default);
}

public record TileTask(
    string MessageId,
    Guid JobId,
    int TileIndex,
    int X,
    int Y,
    ColorFingerprint Fingerprint);
