namespace Opalop.Api.Endpoints;

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Opalop.Application.Interfaces;
using Opalop.Domain.Entities;
using Opalop.Domain.Enums;
using Opalop.Infrastructure.Persistence;
using Opalop.Mosaic.Engine.Analysis;
using SkiaSharp;

public static class PhotoEndpoints
{
    private const string PhotoBucket = "photos";
    private const int TileSize = 94;

    public static void MapPhotoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/photos").RequireAuthorization();

        group.MapPost("/upload", UploadAsync).DisableAntiforgery();
        group.MapGet("/", ListAsync);
        group.MapGet("/{id}/thumbnail", GetThumbnailAsync);
        group.MapDelete("/{id}", DeleteAsync);
    }

    internal static async Task<Guid> GetUserIdAsync(ClaimsPrincipal principal, OpalopDbContext db, CancellationToken ct = default)
    {
        var sub = principal.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("Missing 'sub' claim.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.KeycloakId == sub, ct);
        if (user is not null)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return user.Id;
        }

        var email = principal.FindFirstValue("email")
            ?? principal.FindFirstValue(ClaimTypes.Email)
            ?? $"{sub}@unknown";

        var displayName = principal.FindFirstValue("preferred_username")
            ?? principal.FindFirstValue("name");

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            KeycloakId = sub,
            Email = email,
            DisplayName = displayName,
            LastLoginAt = DateTime.UtcNow
        };

        db.Users.Add(newUser);
        await db.SaveChangesAsync(ct);
        return newUser.Id;
    }

    private static async Task<IResult> UploadAsync(
        IFormFile file,
        ClaimsPrincipal principal,
        OpalopDbContext db,
        IPhotoStorage photoStorage,
        IColorIndex colorIndex,
        CancellationToken ct)
    {
        if (file.Length == 0)
            return Results.BadRequest("No file provided.");

        var userId = await GetUserIdAsync(principal, db, ct);

        await using var stream = file.OpenReadStream();
        using var original = SKBitmap.Decode(stream);
        if (original is null)
            return Results.BadRequest("Invalid image file.");

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
            return Results.BadRequest("Failed to resize image.");
        }

        var fingerprint = QuadrantAnalyzer.Analyze(resized);

        var photoId = Guid.NewGuid();
        var storagePath = $"{userId}/{photoId}.webp";

        using var encodeStream = new MemoryStream();
        using (var image = SKImage.FromBitmap(cropped))
        {
            var data = image.Encode(SKEncodedImageFormat.Webp, 80);
            data.SaveTo(encodeStream);
        }
        encodeStream.Position = 0;

        await photoStorage.UploadAsync(PhotoBucket, storagePath, encodeStream, "image/webp", ct);

        var tilePath = $"{userId}/tiles/{photoId}.webp";
        using var tileStream = new MemoryStream();
        using (var tileImage = SKImage.FromBitmap(resized))
        {
            var tileData = tileImage.Encode(SKEncodedImageFormat.Webp, 80);
            tileData.SaveTo(tileStream);
        }
        tileStream.Position = 0;

        await photoStorage.UploadAsync(PhotoBucket, tilePath, tileStream, "image/webp", ct);

        var photo = new Photo
        {
            Id = photoId,
            UserId = userId,
            Filename = file.FileName,
            Source = PhotoSource.Upload,
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

        await colorIndex.AddPhotoAsync(userId, photoId, fingerprint, ct);

        resized.Dispose();
        cropped.Dispose();

        return Results.Created($"/api/photos/{photoId}", new { photoId });
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal principal,
        OpalopDbContext db,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = await GetUserIdAsync(principal, db, ct);

        var photos = await db.Photos
            .Where(p => p.UserId == userId && p.IsActive)
            .OrderByDescending(p => p.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.Filename,
                p.Source,
                p.UploadedAt
            })
            .ToListAsync(ct);

        var totalCount = await db.Photos.CountAsync(p => p.UserId == userId && p.IsActive, ct);

        return Results.Ok(new { photos, totalCount, page, pageSize });
    }

    private static async Task<IResult> GetThumbnailAsync(
        Guid id,
        ClaimsPrincipal principal,
        OpalopDbContext db,
        IPhotoStorage photoStorage,
        CancellationToken ct)
    {
        var userId = await GetUserIdAsync(principal, db, ct);

        var photo = await db.Photos
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId && p.IsActive, ct);

        if (photo is null)
            return Results.NotFound();

        var url = await photoStorage.GetPresignedUrlAsync(PhotoBucket, photo.StoragePath, 3600, ct);
        return Results.Ok(new { url });
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        ClaimsPrincipal principal,
        OpalopDbContext db,
        IColorIndex colorIndex,
        CancellationToken ct)
    {
        var userId = await GetUserIdAsync(principal, db, ct);

        var photo = await db.Photos
            .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId && p.IsActive, ct);

        if (photo is null)
            return Results.NotFound();

        photo.IsActive = false;
        await db.SaveChangesAsync(ct);

        await colorIndex.RemovePhotoAsync(userId, id, ct);

        return Results.NoContent();
    }
}
