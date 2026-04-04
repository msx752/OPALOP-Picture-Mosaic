namespace Opalop.Api.Services;

using Microsoft.EntityFrameworkCore;
using Opalop.Application.Interfaces;
using Opalop.Domain.Entities;
using Opalop.Domain.Enums;
using Opalop.Domain.ValueObjects;
using Opalop.Infrastructure.Persistence;
using Opalop.Mosaic.Engine.Analysis;
using SkiaSharp;

public class MosaicOrchestrator(
    OpalopDbContext db,
    IPhotoStorage photoStorage,
    IJobTracker jobTracker,
    IMosaicQueue mosaicQueue,
    ILogger<MosaicOrchestrator> logger)
{
    private const string ResourceBucket = "resources";

    public async Task<Guid> GenerateAsync(Guid userId, Guid resourceId, int pxFormat, CancellationToken ct)
    {
        if (!await jobTracker.TryAcquireLockAsync(userId, TimeSpan.FromMinutes(10), ct))
            throw new InvalidOperationException("A mosaic generation job is already running for this user.");

        try
        {
            var resource = await db.Resources
                .FirstOrDefaultAsync(r => r.Id == resourceId && r.UserId == userId, ct)
                ?? throw new InvalidOperationException("Resource not found.");

            await using var sourceStream = await photoStorage.DownloadAsync(ResourceBucket, resource.StoragePath, ct);
            using var sourceBitmap = SKBitmap.Decode(sourceStream);

            if (sourceBitmap is null)
                throw new InvalidOperationException("Failed to decode source image.");

            var pixFmt = PixFormat.From(pxFormat);
            var tiles = TileGridBuilder.Build(sourceBitmap, pixFmt.Size);

            var jobId = Guid.NewGuid();
            var job = new MosaicJob
            {
                Id = jobId,
                UserId = userId,
                ResourceId = resourceId,
                PxFormat = pixFmt,
                TotalTiles = tiles.Count,
                Status = JobStatus.Queued
            };

            db.MosaicJobs.Add(job);
            await db.SaveChangesAsync(ct);

            await jobTracker.InitJobAsync(jobId, tiles.Count, userId, pxFormat, ct);

            var tileTasks = tiles.Select(t => new TileTask(
                MessageId: string.Empty,
                JobId: jobId,
                TileIndex: t.Index,
                X: t.X,
                Y: t.Y,
                Fingerprint: t.Fingerprint
            )).ToList();

            await mosaicQueue.EnqueueTilesAsync(jobId, tileTasks, ct);

            logger.LogInformation("Mosaic job {JobId} created with {TileCount} tiles for user {UserId}",
                jobId, tiles.Count, userId);

            return jobId;
        }
        catch
        {
            await jobTracker.ReleaseLockAsync(userId, ct);
            throw;
        }
    }
}
