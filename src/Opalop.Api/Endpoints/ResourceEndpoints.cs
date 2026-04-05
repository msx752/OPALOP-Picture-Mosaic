namespace Opalop.Api.Endpoints;

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Opalop.Application.Interfaces;
using Opalop.Domain.Entities;
using Opalop.Infrastructure.Persistence;
using SkiaSharp;

public static class ResourceEndpoints
{
    private const string ResourceBucket = "resources";
    private const long MaxFileSizeBytes = 12 * 1024 * 1024; // 12 MB

    public static void MapResourceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/resources").RequireAuthorization();

        group.MapPost("/upload", UploadAsync).DisableAntiforgery();
        group.MapGet("/", ListAsync);
        group.MapGet("/{id}/image", GetImageAsync).AllowAnonymous();
    }

    private static async Task<IResult> GetImageAsync(
        Guid id,
        OpalopDbContext db,
        IPhotoStorage photoStorage,
        CancellationToken ct)
    {
        var resource = await db.Resources.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (resource is null)
            return Results.NotFound();

        var stream = await photoStorage.DownloadAsync(ResourceBucket, resource.StoragePath, ct);
        var contentType = resource.StoragePath.EndsWith(".png") ? "image/png" : "image/jpeg";
        return Results.Stream(stream, contentType);
    }

    private static async Task<IResult> UploadAsync(
        IFormFile file,
        ClaimsPrincipal principal,
        OpalopDbContext db,
        IPhotoStorage photoStorage,
        CancellationToken ct)
    {
        if (file.Length == 0)
            return Results.BadRequest("No file provided.");

        if (file.Length > MaxFileSizeBytes)
            return Results.BadRequest($"File size exceeds maximum of {MaxFileSizeBytes / (1024 * 1024)} MB.");

        var userId = await PhotoEndpoints.GetUserIdAsync(principal, db, ct);

        var bytes = new byte[file.Length];
        await using (var stream = file.OpenReadStream())
            await stream.ReadExactlyAsync(bytes, ct);

        using var bitmap = SKBitmap.Decode(bytes);
        if (bitmap is null)
            return Results.BadRequest("Invalid image file.");

        var width = bitmap.Width;
        var height = bitmap.Height;

        var resourceId = Guid.NewGuid();
        var storagePath = $"{userId}/{resourceId}{Path.GetExtension(file.FileName)}";

        using var uploadStream = new MemoryStream(bytes);
        await photoStorage.UploadAsync(ResourceBucket, storagePath, uploadStream, file.ContentType, ct);

        var resource = new Resource
        {
            Id = resourceId,
            UserId = userId,
            Filename = file.FileName,
            StoragePath = storagePath,
            Width = width,
            Height = height,
            FileSizeBytes = file.Length
        };

        db.Resources.Add(resource);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/resources/{resourceId}", new
        {
            resourceId,
            width,
            height,
            fileSizeBytes = file.Length
        });
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal principal,
        OpalopDbContext db,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = await PhotoEndpoints.GetUserIdAsync(principal, db, ct);

        var resources = await db.Resources
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                r.Id,
                r.Filename,
                r.Width,
                r.Height,
                r.FileSizeBytes,
                r.UploadedAt
            })
            .ToListAsync(ct);

        var totalCount = await db.Resources.CountAsync(r => r.UserId == userId, ct);

        return Results.Ok(new { resources, totalCount, page, pageSize });
    }
}
