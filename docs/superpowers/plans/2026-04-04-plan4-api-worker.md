# Plan 4: API Endpoints & Worker — Photo Upload, Mosaic Generation, SignalR

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the API endpoints for photo upload, mosaic generation, and job status; the Worker's tile processing BackgroundService; the SignalR progress hub; and the MinIO storage implementation — completing the full mosaic generation pipeline.

**Architecture:** API receives requests, analyzes source image, fans out tiles to Redis Stream. Worker consumes tiles, matches colors via Redis, composites via Engine, writes to MinIO. SignalR bridges Redis Pub/Sub to client for real-time progress.

**Tech Stack:** ASP.NET Minimal API, SignalR, StackExchange.Redis, MinIO SDK, SkiaSharp, xUnit

**Spec reference:** `docs/superpowers/specs/2026-04-04-opalop-net9-migration-design.md` — Sections 7, 8

---

## File Map

```
src/Opalop.Infrastructure/
├── Storage/
│   └── MinioPhotoStorage.cs         # IPhotoStorage implementation

src/Opalop.Api/
├── Endpoints/
│   ├── PhotoEndpoints.cs            # /api/photos/*
│   ├── ResourceEndpoints.cs         # /api/resources/*
│   ├── MosaicEndpoints.cs           # /api/mosaic/*
│   └── AccountEndpoints.cs          # /api/account/*
├── Hubs/
│   └── MosaicProgressHub.cs         # SignalR hub
├── Services/
│   └── MosaicOrchestrator.cs        # Orchestrates mosaic generation
├── Program.cs                        # (modify: register endpoints, hub, services)

src/Opalop.Worker/
├── Services/
│   ├── TileProcessorService.cs      # BackgroundService consuming Redis Stream
│   └── StaleMessageClaimerService.cs # Reclaims timed-out tiles
├── Program.cs                        # (modify: register services)
```

---

### Task 1: MinIO Photo Storage Implementation

**Files:**
- Create: `src/Opalop.Infrastructure/Storage/MinioPhotoStorage.cs`
- Modify: `src/Opalop.Infrastructure/ServiceRegistration.cs`

- [ ] **Step 1: Implement MinioPhotoStorage**

```csharp
// src/Opalop.Infrastructure/Storage/MinioPhotoStorage.cs
namespace Opalop.Infrastructure.Storage;

using Minio;
using Minio.DataModel.Args;
using Opalop.Application.Interfaces;

public class MinioPhotoStorage : IPhotoStorage
{
    private readonly IMinioClient _client;

    public MinioPhotoStorage(IMinioClient client) => _client = client;

    public async Task<string> UploadAsync(string bucket, string path, Stream content, string contentType, CancellationToken ct = default)
    {
        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(path)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType), ct);
        return path;
    }

    public async Task<Stream> DownloadAsync(string bucket, string path, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(path)
            .WithCallbackStream(stream => stream.CopyTo(ms)), ct);
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteAsync(string bucket, string path, CancellationToken ct = default)
    {
        await _client.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(bucket)
            .WithObject(path), ct);
    }

    public async Task<string> GetPresignedUrlAsync(string bucket, string path, int expirySeconds = 3600, CancellationToken ct = default)
    {
        return await _client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(path)
            .WithExpiry(expirySeconds));
    }
}
```

- [ ] **Step 2: Register MinIO in ServiceRegistration**

Add to `AddInfrastructure` method:
```csharp
var minioEndpoint = configuration["MinIO:Endpoint"]!;
var minioAccessKey = configuration["MinIO:AccessKey"]!;
var minioSecretKey = configuration["MinIO:SecretKey"]!;
var minioUseSsl = bool.Parse(configuration["MinIO:UseSSL"] ?? "false");

services.AddSingleton<IMinioClient>(_ => new MinioClient()
    .WithEndpoint(minioEndpoint)
    .WithCredentials(minioAccessKey, minioSecretKey)
    .WithSSL(minioUseSsl)
    .Build());
services.AddSingleton<IPhotoStorage, MinioPhotoStorage>();
```

- [ ] **Step 3: Build and commit**

```bash
dotnet build Opalop.sln
git commit -m "feat: add MinioPhotoStorage implementation and register in DI"
```

---

### Task 2: MosaicOrchestrator & API Endpoints

**Files:**
- Create: `src/Opalop.Api/Services/MosaicOrchestrator.cs`
- Create: `src/Opalop.Api/Endpoints/PhotoEndpoints.cs`
- Create: `src/Opalop.Api/Endpoints/ResourceEndpoints.cs`
- Create: `src/Opalop.Api/Endpoints/MosaicEndpoints.cs`
- Create: `src/Opalop.Api/Endpoints/AccountEndpoints.cs`
- Modify: `src/Opalop.Api/Program.cs`

- [ ] **Step 1: Create MosaicOrchestrator**

This service coordinates mosaic generation: validates request, acquires lock, analyzes source, fans out tiles to queue.

```csharp
// src/Opalop.Api/Services/MosaicOrchestrator.cs
namespace Opalop.Api.Services;

using Opalop.Application.Interfaces;
using Opalop.Domain.Entities;
using Opalop.Domain.Enums;
using Opalop.Infrastructure.Persistence;
using Opalop.Mosaic.Engine.Analysis;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

public class MosaicOrchestrator
{
    private readonly OpalopDbContext _db;
    private readonly IPhotoStorage _storage;
    private readonly IMosaicQueue _queue;
    private readonly IJobTracker _tracker;

    public MosaicOrchestrator(OpalopDbContext db, IPhotoStorage storage, IMosaicQueue queue, IJobTracker tracker)
    {
        _db = db;
        _storage = storage;
        _queue = queue;
        _tracker = tracker;
    }

    public async Task<(Guid JobId, string? Error)> StartGenerationAsync(Guid userId, Guid resourceId, int pxFormat, CancellationToken ct)
    {
        // Acquire lock
        if (!await _tracker.TryAcquireLockAsync(userId, TimeSpan.FromMinutes(5), ct))
            return (Guid.Empty, "A mosaic generation is already in progress");

        // Load resource
        var resource = await _db.Resources.FirstOrDefaultAsync(r => r.Id == resourceId && r.UserId == userId, ct);
        if (resource is null)
        {
            await _tracker.ReleaseLockAsync(userId, ct);
            return (Guid.Empty, "Resource not found");
        }

        // Download source image
        using var sourceStream = await _storage.DownloadAsync("resources", resource.StoragePath, ct);
        using var sourceBitmap = SKBitmap.Decode(sourceStream);

        // Build tile grid
        var tiles = TileGridBuilder.Build(sourceBitmap, pxFormat);

        // Create job
        var jobId = Guid.NewGuid();
        var job = new MosaicJob
        {
            Id = jobId,
            UserId = userId,
            ResourceId = resourceId,
            PxFormat = pxFormat,
            TotalTiles = tiles.Count,
            Status = JobStatus.Processing
        };
        _db.MosaicJobs.Add(job);
        await _db.SaveChangesAsync(ct);

        // Init Redis job tracker
        await _tracker.InitJobAsync(jobId, tiles.Count, userId, pxFormat, ct);

        // Fan out tiles to queue
        var tileTasks = tiles.Select(t => new TileTask("", jobId, t.Index, t.X, t.Y, t.Fingerprint)).ToList();
        await _queue.EnqueueTilesAsync(jobId, tileTasks, ct);

        return (jobId, null);
    }
}
```

- [ ] **Step 2: Create PhotoEndpoints**

```csharp
// src/Opalop.Api/Endpoints/PhotoEndpoints.cs
namespace Opalop.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Opalop.Application.Interfaces;
using Opalop.Domain.Entities;
using Opalop.Domain.Enums;
using Opalop.Infrastructure.Persistence;
using Opalop.Mosaic.Engine.Analysis;
using SkiaSharp;
using System.Security.Claims;

public static class PhotoEndpoints
{
    public static void MapPhotoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/photos").RequireAuthorization();

        group.MapPost("/upload", async (HttpRequest request, OpalopDbContext db,
            IPhotoStorage storage, IColorIndex colorIndex, ClaimsPrincipal user) =>
        {
            var userId = GetUserId(user, db).Result;
            if (userId is null) return Results.Unauthorized();

            var file = request.Form.Files.FirstOrDefault();
            if (file is null) return Results.BadRequest("No file provided");

            var photoId = Guid.NewGuid();
            var originalPath = $"{userId}/originals/{photoId}.jpg";
            var tilePath = $"{userId}/tiles/{photoId}.jpg";

            // Upload original
            using var stream = file.OpenReadStream();
            await storage.UploadAsync("photos", originalPath, stream, file.ContentType);

            // Process: square crop + fingerprint
            stream.Position = 0;
            using var bitmap = SKBitmap.Decode(stream);
            var size = Math.Min(bitmap.Width, bitmap.Height);
            var cropX = (bitmap.Width - size) / 2;
            var cropY = (bitmap.Height - size) / 2;

            using var cropped = new SKBitmap(size, size);
            using var canvas = new SKCanvas(cropped);
            canvas.DrawBitmap(bitmap, SKRect.Create(cropX, cropY, size, size), SKRect.Create(0, 0, size, size));

            var fingerprint = QuadrantAnalyzer.Analyze(cropped);

            // Upload tile
            using var tileStream = new MemoryStream();
            cropped.Encode(tileStream, SKEncodedImageFormat.Jpeg, 95);
            tileStream.Position = 0;
            await storage.UploadAsync("photos", tilePath, tileStream, "image/jpeg");

            // Save to DB
            var photo = new Photo
            {
                Id = photoId, UserId = userId.Value, Filename = file.FileName,
                Source = PhotoSource.Upload, StoragePath = originalPath, TilePath = tilePath,
                TotalL = fingerprint.Total.L, TotalA = fingerprint.Total.A, TotalB = fingerprint.Total.B,
                Quadrants = [fingerprint.TopLeft, fingerprint.TopRight, fingerprint.BottomLeft, fingerprint.BottomRight]
            };
            db.Photos.Add(photo);
            await db.SaveChangesAsync();

            // Index in Redis
            await colorIndex.AddPhotoAsync(userId.Value, photoId, fingerprint);

            return Results.Created($"/api/photos/{photoId}", new { photoId });
        });

        group.MapGet("/", async (OpalopDbContext db, ClaimsPrincipal user, int page = 1, int pageSize = 20) =>
        {
            var userId = await GetUserId(user, db);
            if (userId is null) return Results.Unauthorized();

            var photos = await db.Photos
                .Where(p => p.UserId == userId && p.IsActive)
                .OrderByDescending(p => p.UploadedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new { p.Id, p.Filename, p.Source, p.UploadedAt })
                .ToListAsync();

            return Results.Ok(photos);
        });

        group.MapGet("/{id}/thumbnail", async (Guid id, OpalopDbContext db, IPhotoStorage storage, ClaimsPrincipal user) =>
        {
            var userId = await GetUserId(user, db);
            if (userId is null) return Results.Unauthorized();

            var photo = await db.Photos.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            if (photo is null) return Results.NotFound();

            var url = await storage.GetPresignedUrlAsync("photos", photo.TilePath!);
            return Results.Ok(new { url });
        });

        group.MapDelete("/{id}", async (Guid id, OpalopDbContext db, IPhotoStorage storage, IColorIndex colorIndex, ClaimsPrincipal user) =>
        {
            var userId = await GetUserId(user, db);
            if (userId is null) return Results.Unauthorized();

            var photo = await db.Photos.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId);
            if (photo is null) return Results.NotFound();

            photo.IsActive = false;
            await db.SaveChangesAsync();
            await colorIndex.RemovePhotoAsync(userId.Value, id);

            return Results.NoContent();
        });
    }

    private static async Task<Guid?> GetUserId(ClaimsPrincipal user, OpalopDbContext db)
    {
        var keycloakId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? user.FindFirst("sub")?.Value;
        if (keycloakId is null) return null;

        var dbUser = await db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        if (dbUser is null)
        {
            dbUser = new User
            {
                Id = Guid.NewGuid(),
                KeycloakId = keycloakId,
                Email = user.FindFirst(ClaimTypes.Email)?.Value ?? "unknown@opalop.com",
                DisplayName = user.FindFirst("preferred_username")?.Value
            };
            db.Users.Add(dbUser);
            await db.SaveChangesAsync();
        }
        dbUser.LastLoginAt = DateTime.UtcNow;
        return dbUser.Id;
    }
}
```

- [ ] **Step 3: Create ResourceEndpoints**

```csharp
// src/Opalop.Api/Endpoints/ResourceEndpoints.cs
namespace Opalop.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Opalop.Application.Interfaces;
using Opalop.Domain.Entities;
using Opalop.Infrastructure.Persistence;
using SkiaSharp;
using System.Security.Claims;

public static class ResourceEndpoints
{
    public static void MapResourceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/resources").RequireAuthorization();

        group.MapPost("/upload", async (HttpRequest request, OpalopDbContext db,
            IPhotoStorage storage, ClaimsPrincipal user) =>
        {
            var userId = await PhotoEndpoints.GetUserIdPublic(user, db);
            if (userId is null) return Results.Unauthorized();

            var file = request.Form.Files.FirstOrDefault();
            if (file is null) return Results.BadRequest("No file provided");
            if (file.Length > 12 * 1024 * 1024) return Results.BadRequest("File too large (max 12MB)");

            var resourceId = Guid.NewGuid();
            var storagePath = $"{userId}/resources/{resourceId}.jpg";

            using var stream = file.OpenReadStream();
            using var bitmap = SKBitmap.Decode(stream);
            stream.Position = 0;

            await storage.UploadAsync("resources", storagePath, stream, file.ContentType);

            var resource = new Resource
            {
                Id = resourceId, UserId = userId.Value, Filename = file.FileName,
                StoragePath = storagePath, Width = bitmap.Width, Height = bitmap.Height,
                FileSizeBytes = file.Length
            };
            db.Resources.Add(resource);
            await db.SaveChangesAsync();

            return Results.Created($"/api/resources/{resourceId}", new { resourceId });
        });

        group.MapGet("/", async (OpalopDbContext db, ClaimsPrincipal user) =>
        {
            var userId = await PhotoEndpoints.GetUserIdPublic(user, db);
            if (userId is null) return Results.Unauthorized();

            var resources = await db.Resources
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.UploadedAt)
                .Select(r => new { r.Id, r.Filename, r.Width, r.Height, r.UploadedAt })
                .ToListAsync();

            return Results.Ok(resources);
        });
    }
}
```

NOTE: The `GetUserId` helper in PhotoEndpoints needs to be made accessible. Either make it `internal static` and rename to `GetUserIdPublic`, or extract it to a shared helper. The implementer should handle this.

- [ ] **Step 4: Create MosaicEndpoints**

```csharp
// src/Opalop.Api/Endpoints/MosaicEndpoints.cs
namespace Opalop.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Opalop.Application.Interfaces;
using Opalop.Infrastructure.Persistence;
using Opalop.Api.Services;
using System.Security.Claims;

public static class MosaicEndpoints
{
    public static void MapMosaicEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/mosaic").RequireAuthorization();

        group.MapPost("/generate", async (GenerateRequest request, MosaicOrchestrator orchestrator,
            ClaimsPrincipal user, OpalopDbContext db) =>
        {
            var userId = await PhotoEndpoints.GetUserIdPublic(user, db);
            if (userId is null) return Results.Unauthorized();

            var (jobId, error) = await orchestrator.StartGenerationAsync(userId.Value, request.ResourceId, request.PxFormat);
            if (error is not null) return Results.BadRequest(new { error });

            return Results.Accepted($"/api/mosaic/{jobId}/status", new { jobId });
        });

        group.MapGet("/{jobId}/status", async (Guid jobId, IJobTracker tracker) =>
        {
            var info = await tracker.GetJobInfoAsync(jobId);
            if (info is null) return Results.NotFound();

            return Results.Ok(new
            {
                info.JobId, info.Status, info.TotalTiles, info.CompletedTiles,
                Percent = info.TotalTiles > 0 ? (int)(100.0 * info.CompletedTiles / info.TotalTiles) : 0
            });
        });

        group.MapGet("/{jobId}/result", async (Guid jobId, OpalopDbContext db, IPhotoStorage storage) =>
        {
            var job = await db.MosaicJobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job?.ResultPath is null) return Results.NotFound();

            var url = await storage.GetPresignedUrlAsync("mosaics", job.ResultPath);
            return Results.Ok(new { url, job.DurationMs });
        });

        group.MapGet("/history", async (OpalopDbContext db, ClaimsPrincipal user, int page = 1, int pageSize = 20) =>
        {
            var userId = await PhotoEndpoints.GetUserIdPublic(user, db);
            if (userId is null) return Results.Unauthorized();

            var jobs = await db.MosaicJobs
                .Where(j => j.UserId == userId)
                .OrderByDescending(j => j.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(j => new { j.Id, j.Status, j.PxFormat, j.TotalTiles, j.DurationMs, j.CreatedAt })
                .ToListAsync();

            return Results.Ok(jobs);
        });

        group.MapGet("/formats", () => Results.Ok(new[] { 12, 20, 36, 48, 64, 94 })).AllowAnonymous();
    }
}

public record GenerateRequest(Guid ResourceId, int PxFormat = 94);
```

- [ ] **Step 5: Create AccountEndpoints**

```csharp
// src/Opalop.Api/Endpoints/AccountEndpoints.cs
namespace Opalop.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Opalop.Infrastructure.Persistence;
using System.Security.Claims;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/account").RequireAuthorization();

        group.MapGet("/me", async (OpalopDbContext db, ClaimsPrincipal user) =>
        {
            var userId = await PhotoEndpoints.GetUserIdPublic(user, db);
            if (userId is null) return Results.Unauthorized();

            var u = await db.Users.FirstAsync(x => x.Id == userId);
            var photoCount = await db.Photos.CountAsync(p => p.UserId == userId && p.IsActive);

            return Results.Ok(new { u.Id, u.Email, u.DisplayName, u.TicketBalance, PhotoCount = photoCount });
        });

        group.MapGet("/quota", async (OpalopDbContext db, ClaimsPrincipal user) =>
        {
            var userId = await PhotoEndpoints.GetUserIdPublic(user, db);
            if (userId is null) return Results.Unauthorized();

            var u = await db.Users.FirstAsync(x => x.Id == userId);
            return Results.Ok(new { u.TicketBalance });
        });
    }
}
```

- [ ] **Step 6: Create SignalR Hub**

```csharp
// src/Opalop.Api/Hubs/MosaicProgressHub.cs
namespace Opalop.Api.Hubs;

using Microsoft.AspNetCore.SignalR;

public class MosaicProgressHub : Hub
{
    public async Task SubscribeToJob(string jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, jobId);
    }

    public async Task UnsubscribeFromJob(string jobId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, jobId);
    }
}
```

- [ ] **Step 7: Update Api Program.cs**

Add to Program.cs: register MosaicOrchestrator, SignalR, map all endpoint groups, map hub.

```csharp
// Add services
builder.Services.AddScoped<MosaicOrchestrator>();
builder.Services.AddSignalR();

// Map endpoints (after app.UseAuthorization())
app.MapPhotoEndpoints();
app.MapResourceEndpoints();
app.MapMosaicEndpoints();
app.MapAccountEndpoints();
app.MapHub<MosaicProgressHub>("/hubs/mosaic");
```

- [ ] **Step 8: Build and commit**

```bash
dotnet build Opalop.sln
git commit -m "feat: add API endpoints, MosaicOrchestrator, SignalR hub, and MinIO storage"
```

---

### Task 3: Worker TileProcessorService

**Files:**
- Create: `src/Opalop.Worker/Services/TileProcessorService.cs`
- Create: `src/Opalop.Worker/Services/StaleMessageClaimerService.cs`
- Modify: `src/Opalop.Worker/Program.cs`

- [ ] **Step 1: Create TileProcessorService**

```csharp
// src/Opalop.Worker/Services/TileProcessorService.cs
namespace Opalop.Worker.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Opalop.Application.Interfaces;
using Opalop.Mosaic.Engine.Compositing;
using Opalop.Mosaic.Engine.Matching;
using Opalop.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

public class TileProcessorService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IMosaicQueue _queue;
    private readonly IColorIndex _colorIndex;
    private readonly IJobTracker _tracker;
    private readonly IPhotoStorage _storage;
    private readonly ILogger<TileProcessorService> _logger;
    private readonly string _consumerName = $"worker-{Environment.MachineName}-{Guid.NewGuid():N[..8]}";

    public TileProcessorService(IServiceProvider services, IMosaicQueue queue, IColorIndex colorIndex,
        IJobTracker tracker, IPhotoStorage storage, ILogger<TileProcessorService> logger)
    {
        _services = services;
        _queue = queue;
        _colorIndex = colorIndex;
        _tracker = tracker;
        _storage = storage;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("TileProcessor {Consumer} started", _consumerName);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var tile = await _queue.DequeueAsync(_consumerName, ct);
                if (tile is null)
                {
                    await Task.Delay(500, ct);
                    continue;
                }

                await ProcessTileAsync(tile, ct);
                await _queue.AcknowledgeAsync(tile.MessageId, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing tile");
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task ProcessTileAsync(TileTask tile, CancellationToken ct)
    {
        var jobInfo = await _tracker.GetJobInfoAsync(tile.JobId, ct);
        if (jobInfo is null) return;

        // Find best matching photo
        var match = await _colorIndex.FindBestMatchAsync(
            jobInfo.UserId, tile.Fingerprint, tile.JobId, maxUsagePerPhoto: 5, ct);

        using var tileBitmap = new SKBitmap(jobInfo.PxFormat, jobInfo.PxFormat);

        if (match is not null)
        {
            // Download matched photo
            using var photoStream = await _storage.DownloadAsync("photos",
                $"{jobInfo.UserId}/tiles/{match.PhotoId}.jpg", ct);
            using var photoBitmap = SKBitmap.Decode(photoStream);

            // Resize to tile size
            using var resized = photoBitmap.Resize(new SKImageInfo(jobInfo.PxFormat, jobInfo.PxFormat), SKFilterQuality.Medium);

            // Determine rotation
            var rotation = SmartRotator.DetermineRotation(tile.Fingerprint, match.Fingerprint);

            // Composite onto tile
            tileBitmap.Erase(SKColors.Black);
            TileCompositor.Composite(tileBitmap, resized, 0, 0, rotation, opacity: 180);
        }
        else
        {
            // Fallback: fill with average color from fingerprint
            var lab = tile.Fingerprint.Total;
            var (r, g, b) = Opalop.Mosaic.Engine.ColorSpace.LabConverter.LabToRgb(lab);
            tileBitmap.Erase(new SKColor(r, g, b));
        }

        // Upload processed tile
        using var resultStream = new MemoryStream();
        tileBitmap.Encode(resultStream, SKEncodedImageFormat.Png, 100);
        resultStream.Position = 0;
        await _storage.UploadAsync("mosaics", $"jobs/{tile.JobId}/tiles/{tile.TileIndex}.png", resultStream, "image/png", ct);

        // Increment progress
        var completed = await _tracker.IncrementCompletedAsync(tile.JobId, ct);
        _logger.LogInformation("Job {JobId}: tile {Index} done ({Completed}/{Total})",
            tile.JobId, tile.TileIndex, completed, jobInfo.TotalTiles);

        // Check if all tiles are done
        if (completed >= jobInfo.TotalTiles)
        {
            await AssembleMosaicAsync(tile.JobId, jobInfo, ct);
        }
    }

    private async Task AssembleMosaicAsync(Guid jobId, JobInfo jobInfo, CancellationToken ct)
    {
        _logger.LogInformation("Assembling mosaic for job {JobId}", jobId);
        var startTime = DateTime.UtcNow;

        var tiles = new List<ProcessedTile>();
        try
        {
            // Determine canvas size from tile grid
            // We need to figure out the grid layout from the tile positions
            // For simplicity, download all tiles and assemble
            for (int i = 0; i < jobInfo.TotalTiles; i++)
            {
                var stream = await _storage.DownloadAsync("mosaics", $"jobs/{jobId}/tiles/{i}.png", ct);
                var bitmap = SKBitmap.Decode(stream);
                // We need the position info — get it from the original tile task
                // For now, compute from index: row/col based on total tiles and format
                tiles.Add(new ProcessedTile(0, 0, bitmap)); // positions filled below
            }

            // To get proper positions, we use the job info to reconstruct the grid
            // The tiles were indexed in row-major order: index = row * cols + col
            // We need to figure out cols from the source image dimensions
            // This is a simplification — in production, tile positions would be stored
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OpalopDbContext>();
            var job = await db.MosaicJobs.FindAsync([jobId], ct);
            var resource = await db.Resources.FindAsync([job!.ResourceId], ct);

            int cols = resource!.Width / jobInfo.PxFormat;
            int rows = resource.Height / jobInfo.PxFormat;
            int canvasW = cols * jobInfo.PxFormat;
            int canvasH = rows * jobInfo.PxFormat;

            var positionedTiles = tiles.Select((t, i) => new ProcessedTile(
                X: (i % cols) * jobInfo.PxFormat,
                Y: (i / cols) * jobInfo.PxFormat,
                Bitmap: t.Bitmap)).ToList();

            using var result = MosaicAssembler.Assemble(positionedTiles, canvasW, canvasH, jobInfo.PxFormat);

            // Upload final mosaic
            using var resultStream = new MemoryStream();
            result.Encode(resultStream, SKEncodedImageFormat.Jpeg, 90);
            resultStream.Position = 0;
            var resultPath = $"{jobInfo.UserId}/mosaics/{jobId}.jpg";
            await _storage.UploadAsync("mosaics", resultPath, resultStream, "image/jpeg", ct);

            var duration = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            // Update job status
            await _tracker.SetJobCompletedAsync(jobId, resultPath, ct);
            await _tracker.ReleaseLockAsync(jobInfo.UserId, ct);

            // Update DB
            job.Status = Opalop.Domain.Enums.JobStatus.Completed;
            job.ResultPath = resultPath;
            job.DurationMs = duration;
            job.CompletedTiles = jobInfo.TotalTiles;
            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Mosaic {JobId} assembled in {Duration}ms", jobId, duration);
        }
        finally
        {
            foreach (var t in tiles) t.Dispose();
        }
    }
}
```

- [ ] **Step 2: Create StaleMessageClaimerService**

```csharp
// src/Opalop.Worker/Services/StaleMessageClaimerService.cs
namespace Opalop.Worker.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Opalop.Application.Interfaces;

public class StaleMessageClaimerService : BackgroundService
{
    private readonly IMosaicQueue _queue;
    private readonly ILogger<StaleMessageClaimerService> _logger;
    private readonly string _consumerName = $"claimer-{Environment.MachineName}";

    public StaleMessageClaimerService(IMosaicQueue queue, ILogger<StaleMessageClaimerService> logger)
    {
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                await _queue.ClaimStaleMessagesAsync(_consumerName, TimeSpan.FromSeconds(30), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error claiming stale messages");
            }
        }
    }
}
```

- [ ] **Step 3: Update Worker Program.cs**

```csharp
// src/Opalop.Worker/Program.cs
using Opalop.Infrastructure;
using Opalop.Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<TileProcessorService>();
builder.Services.AddHostedService<StaleMessageClaimerService>();

var host = builder.Build();
host.Run();
```

The Worker also needs the SkiaSharp dependency. Add to Worker.csproj if not present:
```xml
<PackageReference Include="SkiaSharp" />
```

- [ ] **Step 4: Build and commit**

```bash
dotnet build Opalop.sln
git commit -m "feat: add Worker TileProcessorService and StaleMessageClaimerService"
```

---

## Summary

| Task | What it produces | Depends on |
|------|-----------------|------------|
| 1 | MinioPhotoStorage + DI registration | — |
| 2 | All API endpoints + MosaicOrchestrator + SignalR hub | Task 1 |
| 3 | Worker TileProcessorService + StaleMessageClaimerService | Task 1 |
