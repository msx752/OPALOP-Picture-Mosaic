namespace Opalop.Infrastructure.Tests.Redis;

using FluentAssertions;
using Opalop.Application.Interfaces;
using Opalop.Domain.ValueObjects;
using Opalop.Infrastructure.Redis;
using Testcontainers.Redis;
using Xunit;

public class RedisColorIndexTests : IAsyncLifetime
{
    private readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:7-alpine").Build();
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

    [Fact]
    public async Task AddPhoto_ThenFindMatch_ReturnsIt()
    {
        var userId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        var fp = MakeFp(50f, 20f, -10f);
        await _index.AddPhotoAsync(userId, photoId, fp);

        var target = MakeFp(52f, 19f, -11f);
        var match = await _index.FindBestMatchAsync(userId, target, Guid.NewGuid());

        match.Should().NotBeNull();
        match!.PhotoId.Should().Be(photoId);
        match.DeltaE.Should().BeLessThan(10f);
    }

    [Fact]
    public async Task FindMatch_NoPhotos_ReturnsNull()
    {
        var match = await _index.FindBestMatchAsync(Guid.NewGuid(), MakeFp(50, 20, -10), Guid.NewGuid());
        match.Should().BeNull();
    }

    [Fact]
    public async Task RemovePhoto_ThenFindMatch_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        await _index.AddPhotoAsync(userId, photoId, MakeFp(50, 20, -10));
        await _index.RemovePhotoAsync(userId, photoId);

        var match = await _index.FindBestMatchAsync(userId, MakeFp(50, 20, -10), Guid.NewGuid());
        match.Should().BeNull();
    }

    [Fact]
    public async Task FindMatch_RespectsMaxUsage()
    {
        var userId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        await _index.AddPhotoAsync(userId, photoId, MakeFp(50, 20, -10));

        var target = MakeFp(50, 20, -10);
        var jobId = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
        {
            var m = await _index.FindBestMatchAsync(userId, target, jobId, maxUsagePerPhoto: 5);
            m.Should().NotBeNull();
        }

        var exhausted = await _index.FindBestMatchAsync(userId, target, jobId, maxUsagePerPhoto: 5);
        exhausted.Should().BeNull();
    }

    [Fact]
    public async Task RebuildIndex_ReplacesExistingData()
    {
        var userId = Guid.NewGuid();
        await _index.AddPhotoAsync(userId, Guid.NewGuid(), MakeFp(50, 20, -10));

        var newPhoto = Guid.NewGuid();
        await _index.RebuildIndexAsync(userId, [(newPhoto, MakeFp(70, 10, 5))]);

        var match = await _index.FindBestMatchAsync(userId, MakeFp(70, 10, 5), Guid.NewGuid());
        match.Should().NotBeNull();
        match!.PhotoId.Should().Be(newPhoto);
    }

    private static ColorFingerprint MakeFp(float l, float a, float b)
    {
        var q = new QuadrantLab(l, a, b);
        return new ColorFingerprint(q, q, q, q, q);
    }
}
