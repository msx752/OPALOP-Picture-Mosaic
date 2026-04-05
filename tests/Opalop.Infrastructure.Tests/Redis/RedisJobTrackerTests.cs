namespace Opalop.Infrastructure.Tests.Redis;

using FluentAssertions;
using Opalop.Infrastructure.Redis;
using Testcontainers.Redis;
using Xunit;

public class RedisJobTrackerTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:7-alpine").Build();
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
        await _tracker.InitJobAsync(jobId, 64, Guid.NewGuid(), 94, 128, 0, null);
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
        await _tracker.InitJobAsync(jobId, 10, Guid.NewGuid(), 94, 128, 0, null);
        (await _tracker.IncrementCompletedAsync(jobId)).Should().Be(1);
        (await _tracker.IncrementCompletedAsync(jobId)).Should().Be(2);
    }

    [Fact]
    public async Task TryAcquireLock_FirstCallTrue_SecondCallFalse()
    {
        var userId = Guid.NewGuid();
        (await _tracker.TryAcquireLockAsync(userId, TimeSpan.FromMinutes(5))).Should().BeTrue();
        (await _tracker.TryAcquireLockAsync(userId, TimeSpan.FromMinutes(5))).Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseLock_ThenAcquire_Succeeds()
    {
        var userId = Guid.NewGuid();
        await _tracker.TryAcquireLockAsync(userId, TimeSpan.FromMinutes(5));
        await _tracker.ReleaseLockAsync(userId);
        (await _tracker.TryAcquireLockAsync(userId, TimeSpan.FromMinutes(5))).Should().BeTrue();
    }

    [Fact]
    public async Task SetJobCompleted_UpdatesStatus()
    {
        var jobId = Guid.NewGuid();
        await _tracker.InitJobAsync(jobId, 10, Guid.NewGuid(), 94, 128, 0, null);
        await _tracker.SetJobCompletedAsync(jobId, "mosaics/result.jpg");
        var info = await _tracker.GetJobInfoAsync(jobId);
        info!.Status.Should().Be("completed");
    }

    [Fact]
    public async Task SetJobFailed_UpdatesStatus()
    {
        var jobId = Guid.NewGuid();
        await _tracker.InitJobAsync(jobId, 10, Guid.NewGuid(), 94, 128, 0, null);
        await _tracker.SetJobFailedAsync(jobId, "Something went wrong");
        var info = await _tracker.GetJobInfoAsync(jobId);
        info!.Status.Should().Be("failed");
    }
}
