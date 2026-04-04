namespace Opalop.Infrastructure.Tests.Redis;

using FluentAssertions;
using Opalop.Application.Interfaces;
using Opalop.Domain.ValueObjects;
using Opalop.Infrastructure.Redis;
using Testcontainers.Redis;
using Xunit;

public class RedisMosaicQueueTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:7-alpine").Build();
    private RedisConnectionManager _manager = null!;
    private RedisMosaicQueue _queue = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        _manager = new RedisConnectionManager(_redis.GetConnectionString());
        _queue = new RedisMosaicQueue(_manager);
    }

    public async Task DisposeAsync() { _manager.Dispose(); await _redis.DisposeAsync(); }

    [Fact]
    public async Task EnqueueAndDequeue_ReturnsTile()
    {
        var jobId = Guid.NewGuid();
        var fp = MakeFp(50, 20, -10);
        await _queue.EnqueueTilesAsync(jobId, [new("", jobId, 0, 0, 0, fp), new("", jobId, 1, 100, 0, fp)]);
        var tile = await _queue.DequeueAsync("worker-1");
        tile.Should().NotBeNull();
        tile!.JobId.Should().Be(jobId);
    }

    [Fact]
    public async Task Dequeue_EmptyQueue_ReturnsNull()
    {
        var tile = await _queue.DequeueAsync("worker-empty");
        tile.Should().BeNull();
    }

    [Fact]
    public async Task Acknowledge_CompletesSuccessfully()
    {
        var jobId = Guid.NewGuid();
        await _queue.EnqueueTilesAsync(jobId, [new("", jobId, 0, 0, 0, MakeFp(50, 20, -10))]);
        var tile = await _queue.DequeueAsync("worker-ack");
        tile.Should().NotBeNull();
        await _queue.AcknowledgeAsync(tile!.MessageId);
    }

    private static ColorFingerprint MakeFp(float l, float a, float b)
    {
        var q = new QuadrantLab(l, a, b);
        return new ColorFingerprint(q, q, q, q, q);
    }
}
