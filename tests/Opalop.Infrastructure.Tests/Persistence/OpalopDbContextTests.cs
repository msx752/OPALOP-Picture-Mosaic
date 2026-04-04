namespace Opalop.Infrastructure.Tests.Persistence;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Opalop.Domain.Entities;
using Opalop.Domain.Enums;
using Opalop.Domain.ValueObjects;
using Opalop.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

public class OpalopDbContextTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    private OpalopDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OpalopDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        var ctx = new OpalopDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    [Fact]
    public async Task CanInsertAndQueryUser()
    {
        await using var ctx = CreateContext();
        var user = new User { Id = Guid.NewGuid(), KeycloakId = "kc-123", Email = "test@example.com", DisplayName = "Test User" };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        var loaded = await ctx.Users.FirstAsync(u => u.KeycloakId == "kc-123");
        loaded.Email.Should().Be("test@example.com");
        loaded.TicketBalance.Should().Be(100);
    }

    [Fact]
    public async Task CanInsertPhotoWithQuadrants()
    {
        await using var ctx = CreateContext();
        var user = new User { Id = Guid.NewGuid(), KeycloakId = "kc-456", Email = "photo@test.com" };
        ctx.Users.Add(user);

        var photo = new Photo
        {
            Id = Guid.NewGuid(), UserId = user.Id, Filename = "test.jpg",
            Source = PhotoSource.Upload, StoragePath = "photos/originals/test.jpg",
            TilePath = "photos/tiles/test.jpg", TotalL = 52.3f, TotalA = 28.1f, TotalB = -14.7f,
            Quadrants = [
                new QuadrantLab(61.2f, 32.4f, -10.2f), new QuadrantLab(48.9f, 25.7f, -18.3f),
                new QuadrantLab(55.1f, 30.0f, -12.1f), new QuadrantLab(44.0f, 24.2f, -18.2f)
            ]
        };
        ctx.Photos.Add(photo);
        await ctx.SaveChangesAsync();

        var loaded = await ctx.Photos.FirstAsync(p => p.Filename == "test.jpg");
        loaded.TotalL.Should().Be(52.3f);
        loaded.Quadrants.Should().HaveCount(4);
        loaded.Quadrants[0].L.Should().Be(61.2f);
    }

    [Fact]
    public async Task CanInsertMosaicJobWithResourceLink()
    {
        await using var ctx = CreateContext();
        var user = new User { Id = Guid.NewGuid(), KeycloakId = "kc-789", Email = "job@test.com" };
        var resource = new Resource { Id = Guid.NewGuid(), UserId = user.Id, Filename = "source.jpg", StoragePath = "resources/source.jpg", Width = 1200, Height = 800, FileSizeBytes = 500_000 };
        var job = new MosaicJob { Id = Guid.NewGuid(), UserId = user.Id, ResourceId = resource.Id, PxFormat = PixFormat.From(94), TotalTiles = 64 };

        ctx.Users.Add(user);
        ctx.Resources.Add(resource);
        ctx.MosaicJobs.Add(job);
        await ctx.SaveChangesAsync();

        var loaded = await ctx.MosaicJobs.Include(j => j.Resource).FirstAsync(j => j.Id == job.Id);
        loaded.Status.Should().Be(JobStatus.Queued);
        loaded.Resource.Width.Should().Be(1200);
    }
}
