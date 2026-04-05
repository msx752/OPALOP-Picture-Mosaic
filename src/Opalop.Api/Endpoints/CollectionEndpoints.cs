namespace Opalop.Api.Endpoints;

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Opalop.Application.Interfaces;
using Opalop.Domain.Entities;
using Opalop.Infrastructure.Persistence;

public static class CollectionEndpoints
{
    public static void MapCollectionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/collections").RequireAuthorization();

        group.MapPost("/", CreateAsync);
        group.MapGet("/", ListAsync);
        group.MapGet("/{id}", GetAsync);
        group.MapPut("/{id}", UpdateAsync);
        group.MapDelete("/{id}", DeleteAsync);
        group.MapPost("/{id}/photos", AddPhotosAsync);
        group.MapDelete("/{id}/photos/{photoId}", RemovePhotoAsync);
    }

    private static async Task<IResult> CreateAsync(
        CreateCollectionRequest request,
        ClaimsPrincipal principal,
        OpalopDbContext db,
        CancellationToken ct)
    {
        var userId = await PhotoEndpoints.GetUserIdAsync(principal, db, ct);

        var collection = new PhotoCollection
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            Description = request.Description
        };

        db.PhotoCollections.Add(collection);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/collections/{collection.Id}", new
        {
            collection.Id,
            collection.Name,
            collection.Description,
            photoCount = 0
        });
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal principal,
        OpalopDbContext db,
        CancellationToken ct)
    {
        var userId = await PhotoEndpoints.GetUserIdAsync(principal, db, ct);

        var collections = await db.PhotoCollections
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Description,
                PhotoCount = c.Photos.Count(p => p.IsActive),
                c.CreatedAt
            })
            .ToListAsync(ct);

        return Results.Ok(new { collections });
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        ClaimsPrincipal principal,
        OpalopDbContext db,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        var userId = await PhotoEndpoints.GetUserIdAsync(principal, db, ct);

        var collection = await db.PhotoCollections
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (collection is null)
            return Results.NotFound();

        var photos = await db.Photos
            .Where(p => p.CollectionId == id && p.IsActive)
            .OrderByDescending(p => p.UploadedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new { p.Id, p.Filename, p.Source, p.UploadedAt })
            .ToListAsync(ct);

        var totalCount = await db.Photos.CountAsync(p => p.CollectionId == id && p.IsActive, ct);

        return Results.Ok(new
        {
            collection.Id,
            collection.Name,
            collection.Description,
            photos,
            totalCount,
            page,
            pageSize
        });
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateCollectionRequest request,
        ClaimsPrincipal principal,
        OpalopDbContext db,
        CancellationToken ct)
    {
        var userId = await PhotoEndpoints.GetUserIdAsync(principal, db, ct);

        var collection = await db.PhotoCollections
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (collection is null)
            return Results.NotFound();

        collection.Name = request.Name ?? collection.Name;
        collection.Description = request.Description ?? collection.Description;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { collection.Id, collection.Name, collection.Description });
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        ClaimsPrincipal principal,
        OpalopDbContext db,
        CancellationToken ct)
    {
        var userId = await PhotoEndpoints.GetUserIdAsync(principal, db, ct);

        var collection = await db.PhotoCollections
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (collection is null)
            return Results.NotFound();

        // Unassign photos from collection (don't delete them)
        await db.Photos.Where(p => p.CollectionId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CollectionId, (Guid?)null), ct);

        db.PhotoCollections.Remove(collection);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static async Task<IResult> AddPhotosAsync(
        Guid id,
        AddPhotosRequest request,
        ClaimsPrincipal principal,
        OpalopDbContext db,
        CancellationToken ct)
    {
        var userId = await PhotoEndpoints.GetUserIdAsync(principal, db, ct);

        var collection = await db.PhotoCollections
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (collection is null)
            return Results.NotFound();

        var updated = await db.Photos
            .Where(p => request.PhotoIds.Contains(p.Id) && p.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CollectionId, id), ct);

        return Results.Ok(new { added = updated });
    }

    private static async Task<IResult> RemovePhotoAsync(
        Guid id,
        Guid photoId,
        ClaimsPrincipal principal,
        OpalopDbContext db,
        CancellationToken ct)
    {
        var userId = await PhotoEndpoints.GetUserIdAsync(principal, db, ct);

        var updated = await db.Photos
            .Where(p => p.Id == photoId && p.CollectionId == id && p.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CollectionId, (Guid?)null), ct);

        return updated > 0 ? Results.NoContent() : Results.NotFound();
    }
}

public record CreateCollectionRequest(string Name, string? Description = null);
public record UpdateCollectionRequest(string? Name = null, string? Description = null);
public record AddPhotosRequest(List<Guid> PhotoIds);
