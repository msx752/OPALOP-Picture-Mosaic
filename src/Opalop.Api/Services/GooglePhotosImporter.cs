namespace Opalop.Api.Services;

using Microsoft.EntityFrameworkCore;
using Opalop.Application.Interfaces;
using Opalop.Domain.Entities;
using Opalop.Domain.Enums;
using Opalop.Infrastructure.Persistence;
using Opalop.Mosaic.Engine.Analysis;
using SkiaSharp;

public class GooglePhotosImporter(
    IHttpClientFactory httpClientFactory,
    OpalopDbContext db,
    IPhotoStorage photoStorage,
    IColorIndex colorIndex,
    ILogger<GooglePhotosImporter> logger)
{
    private const string PhotoBucket = "photos";
    private const int TileSize = 94;

    public async Task<int> ImportAsync(Guid userId, IReadOnlyList<string> photoUrls, CancellationToken ct = default)
    {
        var httpClient = httpClientFactory.CreateClient("GooglePhotos");
        int imported = 0;

        foreach (var url in photoUrls)
        {
            try
            {
                imported += await ImportOneAsync(userId, url, httpClient, ct) ? 1 : 0;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to import photo from URL: {Url}", url);
            }
        }

        return imported;
    }

    private async Task<bool> ImportOneAsync(Guid userId, string url, HttpClient httpClient, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(ct);
        using var original = SKBitmap.Decode(responseStream);
        if (original is null)
        {
            logger.LogWarning("Could not decode image from URL: {Url}", url);
            return false;
        }

        // Square-crop (center)
        var side = Math.Min(original.Width, original.Height);
        var cropped = new SKBitmap(side, side);
        using (var canvas = new SKCanvas(cropped))
        {
            var srcX = (original.Width - side) / 2;
            var srcY = (original.Height - side) / 2;
            canvas.DrawBitmap(original,
                SKRect.Create(srcX, srcY, side, side),
                SKRect.Create(0, 0, side, side));
        }

        var resized = cropped.Resize(new SKImageInfo(TileSize, TileSize), SKSamplingOptions.Default);
        if (resized is null)
        {
            cropped.Dispose();
            logger.LogWarning("Failed to resize image from URL: {Url}", url);
            return false;
        }

        var fingerprint = QuadrantAnalyzer.Analyze(resized);

        var photoId = Guid.NewGuid();
        var filename = $"google_{photoId}.webp";
        var storagePath = $"{userId}/{photoId}.webp";
        var tilePath = $"{userId}/tiles/{photoId}.webp";

        // Upload original (square-cropped)
        using var encodeStream = new MemoryStream();
        using (var image = SKImage.FromBitmap(cropped))
        {
            var data = image.Encode(SKEncodedImageFormat.Webp, 80);
            data.SaveTo(encodeStream);
        }
        encodeStream.Position = 0;
        await photoStorage.UploadAsync(PhotoBucket, storagePath, encodeStream, "image/webp", ct);

        // Upload tile
        using var tileStream = new MemoryStream();
        using (var tileImage = SKImage.FromBitmap(resized))
        {
            var tileData = tileImage.Encode(SKEncodedImageFormat.Webp, 80);
            tileData.SaveTo(tileStream);
        }
        tileStream.Position = 0;
        await photoStorage.UploadAsync(PhotoBucket, tilePath, tileStream, "image/webp", ct);

        // Save to DB
        var photo = new Photo
        {
            Id = photoId,
            UserId = userId,
            Filename = filename,
            Source = PhotoSource.Google,
            StoragePath = storagePath,
            TilePath = tilePath,
            TotalL = fingerprint.Total.L,
            TotalA = fingerprint.Total.A,
            TotalB = fingerprint.Total.B,
            Quadrants =
            [
                fingerprint.TopLeft,
                fingerprint.TopRight,
                fingerprint.BottomLeft,
                fingerprint.BottomRight
            ]
        };

        db.Photos.Add(photo);
        await db.SaveChangesAsync(ct);

        // Index in Redis
        await colorIndex.AddPhotoAsync(userId, photoId, fingerprint, ct: ct);

        resized.Dispose();
        cropped.Dispose();

        logger.LogInformation("Imported Google photo {PhotoId} for user {UserId}", photoId, userId);
        return true;
    }
}
