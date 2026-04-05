namespace Opalop.Worker.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Opalop.Application.Interfaces;
using Opalop.Domain.Enums;
using Opalop.Infrastructure.Persistence;
using Opalop.Mosaic.Engine.Analysis;
using Opalop.Mosaic.Engine.ColorSpace;
using Opalop.Mosaic.Engine.Compositing;
using Opalop.Mosaic.Engine.Matching;
using SkiaSharp;

public sealed class TileProcessorService(
    IMosaicQueue queue,
    IJobTracker jobTracker,
    IColorIndex colorIndex,
    IPhotoStorage photoStorage,
    IServiceProvider serviceProvider,
    ILogger<TileProcessorService> logger) : BackgroundService
{
    private const string PhotoBucket = "photos";
    private const string MosaicBucket = "mosaics";
    private static readonly string ConsumerName = $"worker-{Environment.MachineName}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TileProcessorService started. Consumer: {Consumer}", ConsumerName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tile = await queue.DequeueAsync(ConsumerName, stoppingToken);
                if (tile is null)
                {
                    await Task.Delay(500, stoppingToken);
                    continue;
                }

                await ProcessTileAsync(tile, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing tile");
                await Task.Delay(1000, stoppingToken);
            }
        }

        logger.LogInformation("TileProcessorService stopped");
    }

    private async Task ProcessTileAsync(TileTask tile, CancellationToken ct)
    {
        var jobInfo = await jobTracker.GetJobInfoAsync(tile.JobId, ct);
        if (jobInfo is null)
        {
            logger.LogWarning("Job {JobId} not found, skipping tile {TileIndex}", tile.JobId, tile.TileIndex);
            await queue.AcknowledgeAsync(tile.MessageId, ct);
            return;
        }

        var pxFormat = jobInfo.PxFormat;

        try
        {
            using var tileBitmap = await CreateTileBitmapAsync(tile, jobInfo, pxFormat, ct);

            await UploadTileBitmapAsync(tile.JobId, tile.TileIndex, tileBitmap, ct);
            await queue.AcknowledgeAsync(tile.MessageId, ct);

            var completed = await jobTracker.IncrementCompletedAsync(tile.JobId, ct);

            logger.LogDebug("Tile {TileIndex} for job {JobId} completed ({Completed}/{Total})",
                tile.TileIndex, tile.JobId, completed, jobInfo.TotalTiles);

            if (completed == jobInfo.TotalTiles)
            {
                await AssembleMosaicAsync(tile.JobId, jobInfo, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process tile {TileIndex} for job {JobId}",
                tile.TileIndex, tile.JobId);
            throw;
        }
    }

    private async Task<SKBitmap> CreateTileBitmapAsync(
        TileTask tile, JobInfo jobInfo, int pxFormat, CancellationToken ct)
    {
        int tileRow = pxFormat > 0 ? tile.Y / pxFormat : -1;
        int tileCol = pxFormat > 0 ? tile.X / pxFormat : -1;

        var match = await colorIndex.FindBestMatchAsync(
            jobInfo.UserId, tile.Fingerprint, tile.JobId,
            maxUsagePerPhoto: 5, tileRow: tileRow, tileCol: tileCol,
            minDistance: 3, collectionId: jobInfo.CollectionId, ct: ct);

        if (match is not null)
        {
            return await CreateMatchedTileAsync(match, tile.Fingerprint, pxFormat, ct);
        }

        return CreateSolidColorTile(tile.Fingerprint, pxFormat);
    }

    /// <summary>
    /// DeltaE threshold for "weak match" — above this, apply color-fill base + higher opacity overlay.
    /// This mirrors the legacy fallback behavior where poorly matched tiles get a target-colored
    /// background with the photo drawn at increased opacity (+20%).
    /// </summary>
    private const float WeakMatchThreshold = 40f;

    private async Task<SKBitmap> CreateMatchedTileAsync(
        ColorMatchResult match, Domain.ValueObjects.ColorFingerprint targetFingerprint,
        int pxFormat, CancellationToken ct)
    {
        Stream photoStream;

        try
        {
            var tileStoragePath = await FindPhotoTilePathAsync(match.PhotoId, ct);
            if (tileStoragePath is not null)
            {
                photoStream = await photoStorage.DownloadAsync(PhotoBucket, tileStoragePath, ct);
            }
            else
            {
                var storagePath = await FindPhotoStoragePathAsync(match.PhotoId, ct);
                if (storagePath is null)
                    return CreateSolidColorTile(targetFingerprint, pxFormat);
                photoStream = await photoStorage.DownloadAsync(PhotoBucket, storagePath, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to download photo {PhotoId}, using solid color fallback", match.PhotoId);
            return CreateSolidColorTile(targetFingerprint, pxFormat);
        }

        await using (photoStream)
        {
            using var photoBitmap = SKBitmap.Decode(photoStream);
            if (photoBitmap is null)
                return CreateSolidColorTile(targetFingerprint, pxFormat);

            var resized = photoBitmap.Resize(new SKImageInfo(pxFormat, pxFormat), SKSamplingOptions.Default);
            if (resized is null)
                return CreateSolidColorTile(targetFingerprint, pxFormat);

            var rotation = SmartRotator.DetermineRotation(targetFingerprint, match.Fingerprint);
            var isWeakMatch = match.DeltaE > WeakMatchThreshold;

            if (isWeakMatch)
            {
                // Legacy fallback: fill with target color, then draw tile at higher opacity
                var (r, g, b) = LabConverter.LabToRgb(targetFingerprint.Total);
                var canvas = new SKBitmap(pxFormat, pxFormat);
                using var canvasGraphics = new SKCanvas(canvas);
                canvasGraphics.Clear(new SKColor(r, g, b));

                // Draw photo at ~70% opacity on top of color fill (legacy: opacity + 20)
                TileCompositor.Composite(canvas, resized, 0, 0, rotation, opacity: 179);
                resized.Dispose();
                return canvas;
            }

            if (rotation == RotationAngle.None)
                return resized;

            var rotatedCanvas = new SKBitmap(pxFormat, pxFormat);
            TileCompositor.Composite(rotatedCanvas, resized, 0, 0, rotation, opacity: 255);
            resized.Dispose();
            return rotatedCanvas;
        }
    }

    private static SKBitmap CreateSolidColorTile(
        Domain.ValueObjects.ColorFingerprint fingerprint, int pxFormat)
    {
        var (r, g, b) = LabConverter.LabToRgb(fingerprint.Total);
        var bitmap = new SKBitmap(pxFormat, pxFormat);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(r, g, b));
        return bitmap;
    }

    private async Task UploadTileBitmapAsync(Guid jobId, int tileIndex, SKBitmap bitmap, CancellationToken ct)
    {
        using var image = SKImage.FromBitmap(bitmap);
        var data = image.Encode(SKEncodedImageFormat.Png, 100);

        using var stream = new MemoryStream();
        data.SaveTo(stream);
        stream.Position = 0;

        var path = $"jobs/{jobId}/tiles/{tileIndex}.png";
        await photoStorage.UploadAsync(MosaicBucket, path, stream, "image/png", ct);
    }

    private async Task AssembleMosaicAsync(Guid jobId, JobInfo jobInfo, CancellationToken ct)
    {
        logger.LogInformation("All tiles completed for job {JobId}. Assembling mosaic...", jobId);

        var startTime = DateTime.UtcNow;
        var processedTiles = new List<ProcessedTile>();

        try
        {
            string resourceStoragePath;
            int canvasWidth;
            int canvasHeight;

            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<OpalopDbContext>();
                var job = await db.MosaicJobs
                    .Include(j => j.Resource)
                    .FirstOrDefaultAsync(j => j.Id == jobId, ct);

                if (job is null)
                {
                    logger.LogError("Job {JobId} not found in database during assembly", jobId);
                    await jobTracker.SetJobFailedAsync(jobId, "Job not found in database", ct);
                    return;
                }

                canvasWidth = job.Resource.Width;
                canvasHeight = job.Resource.Height;
                resourceStoragePath = job.Resource.StoragePath;
            }

            var pxFormat = jobInfo.PxFormat;
            // Calculate upscaled + tile-aligned canvas dimensions (matches TileGridBuilder.Build)
            var gridDims = TileGridBuilder.CalculateDimensions(canvasWidth, canvasHeight, pxFormat);
            var colCount = gridDims.Width / pxFormat;
            var rowCount = gridDims.Height / pxFormat;

            for (var i = 0; i < jobInfo.TotalTiles; i++)
            {
                var tilePath = $"jobs/{jobId}/tiles/{i}.png";
                await using var tileStream = await photoStorage.DownloadAsync(MosaicBucket, tilePath, ct);
                var tileBitmap = SKBitmap.Decode(tileStream);

                if (tileBitmap is null)
                {
                    logger.LogWarning("Failed to decode tile {TileIndex} for job {JobId}", i, jobId);
                    continue;
                }

                var col = i % colCount;
                var row = i / colCount;
                processedTiles.Add(new ProcessedTile(col * pxFormat, row * pxFormat, tileBitmap));
            }

            var isClassic = jobInfo.Style == (int)Domain.Enums.MosaicStyle.Classic;
            SKBitmap? sourceBitmap = null;

            if (!isClassic)
            {
                try
                {
                    await using var sourceStream = await photoStorage.DownloadAsync("resources", resourceStoragePath, ct);
                    sourceBitmap = SKBitmap.Decode(sourceStream);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to download source image for overlay, falling back to classic mode");
                }
            }

            using var mosaicBitmap = (isClassic || sourceBitmap is null)
                ? MosaicAssembler.AssembleClassic(processedTiles, gridDims.Width, gridDims.Height)
                : MosaicAssembler.Assemble(processedTiles, sourceBitmap, gridDims.Width, gridDims.Height, jobInfo.Opacity);
            sourceBitmap?.Dispose();

            using var mosaicImage = SKImage.FromBitmap(mosaicBitmap);
            var encodedData = mosaicImage.Encode(SKEncodedImageFormat.Jpeg, 90);

            using var mosaicStream = new MemoryStream();
            encodedData.SaveTo(mosaicStream);
            mosaicStream.Position = 0;

            var resultPath = $"{jobInfo.UserId}/mosaics/{jobId}.jpg";
            await photoStorage.UploadAsync(MosaicBucket, resultPath, mosaicStream, "image/jpeg", ct);

            var durationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

            await jobTracker.SetJobCompletedAsync(jobId, resultPath, ct);

            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<OpalopDbContext>();
                var job = await db.MosaicJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
                if (job is not null)
                {
                    job.Status = JobStatus.Completed;
                    job.ResultPath = resultPath;
                    job.CompletedTiles = jobInfo.TotalTiles;
                    job.CompletedAt = DateTime.UtcNow;
                    job.DurationMs = durationMs;
                    await db.SaveChangesAsync(ct);
                }
            }

            await jobTracker.ReleaseLockAsync(jobInfo.UserId, ct);

            logger.LogInformation("Mosaic assembled for job {JobId} in {Duration}ms. Path: {ResultPath}",
                jobId, durationMs, resultPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to assemble mosaic for job {JobId}", jobId);

            await jobTracker.SetJobFailedAsync(jobId, ex.Message, ct);

            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<OpalopDbContext>();
                var job = await db.MosaicJobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
                if (job is not null)
                {
                    job.Status = JobStatus.Failed;
                    job.ErrorMessage = ex.Message;
                    job.CompletedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                }
            }

            await jobTracker.ReleaseLockAsync(jobInfo.UserId, ct);
        }
        finally
        {
            foreach (var tile in processedTiles)
                tile.Dispose();
        }
    }

    private async Task<string?> FindPhotoTilePathAsync(Guid photoId, CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpalopDbContext>();
        var photo = await db.Photos.FirstOrDefaultAsync(p => p.Id == photoId, ct);
        return photo?.TilePath;
    }

    private async Task<string?> FindPhotoStoragePathAsync(Guid photoId, CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OpalopDbContext>();
        var photo = await db.Photos.FirstOrDefaultAsync(p => p.Id == photoId, ct);
        return photo?.StoragePath;
    }
}
