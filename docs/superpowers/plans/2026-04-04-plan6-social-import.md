# Plan 6: Social Import — Google Photos OAuth + Import Pipeline

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement Google Photos OAuth integration allowing users to connect their Google account and import photos into their mosaic library.

**Architecture:** OAuth2 authorization code flow via Keycloak identity brokering or direct Google API. Photos are downloaded, square-cropped, fingerprinted, and indexed — same pipeline as manual upload.

**Tech Stack:** Google Photos API, OAuth2, HttpClient, Keycloak

---

### Task 1: Google Photos Import Endpoints + Service

**Files:**
- Create: `src/Opalop.Api/Services/GooglePhotosImporter.cs`
- Create: `src/Opalop.Api/Endpoints/ImportEndpoints.cs`
- Modify: `src/Opalop.Api/Program.cs`

- [ ] **Step 1: Create GooglePhotosImporter service**

A service that:
- Stores/retrieves Google OAuth tokens from the `social_connections` table
- Lists photos from Google Photos API using the access token
- Downloads selected photos, processes them through the same upload pipeline (square crop, fingerprint, Redis index, MinIO storage, DB record)

```csharp
// src/Opalop.Api/Services/GooglePhotosImporter.cs
namespace Opalop.Api.Services;

using Microsoft.EntityFrameworkCore;
using Opalop.Application.Interfaces;
using Opalop.Domain.Entities;
using Opalop.Domain.Enums;
using Opalop.Infrastructure.Persistence;
using Opalop.Mosaic.Engine.Analysis;
using SkiaSharp;

public class GooglePhotosImporter
{
    private readonly OpalopDbContext _db;
    private readonly IPhotoStorage _storage;
    private readonly IColorIndex _colorIndex;
    private readonly HttpClient _http;

    public GooglePhotosImporter(OpalopDbContext db, IPhotoStorage storage, IColorIndex colorIndex, IHttpClientFactory httpFactory)
    {
        _db = db;
        _storage = storage;
        _colorIndex = colorIndex;
        _http = httpFactory.CreateClient("GooglePhotos");
    }

    public async Task<int> ImportPhotosAsync(Guid userId, IReadOnlyList<string> photoUrls, CancellationToken ct)
    {
        int imported = 0;
        foreach (var url in photoUrls)
        {
            try
            {
                var response = await _http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode) continue;

                using var imageStream = await response.Content.ReadAsStreamAsync(ct);
                using var bitmap = SKBitmap.Decode(imageStream);
                if (bitmap is null) continue;

                // Square crop (center)
                var size = Math.Min(bitmap.Width, bitmap.Height);
                var cropX = (bitmap.Width - size) / 2;
                var cropY = (bitmap.Height - size) / 2;
                using var cropped = new SKBitmap(size, size);
                using var canvas = new SKCanvas(cropped);
                canvas.DrawBitmap(bitmap, SKRect.Create(cropX, cropY, size, size), SKRect.Create(0, 0, size, size));

                var fingerprint = QuadrantAnalyzer.Analyze(cropped);
                var photoId = Guid.NewGuid();
                var tilePath = $"{userId}/tiles/{photoId}.jpg";
                var originalPath = $"{userId}/originals/{photoId}.jpg";

                // Upload tile
                using var tileStream = new MemoryStream();
                cropped.Encode(tileStream, SKEncodedImageFormat.Jpeg, 95);
                tileStream.Position = 0;
                await _storage.UploadAsync("photos", tilePath, tileStream, "image/jpeg", ct);

                // Upload original
                imageStream.Position = 0;
                await _storage.UploadAsync("photos", originalPath, imageStream, "image/jpeg", ct);

                // Save to DB
                _db.Photos.Add(new Photo
                {
                    Id = photoId, UserId = userId,
                    Filename = $"google_{photoId:N}.jpg",
                    Source = PhotoSource.Google,
                    StoragePath = originalPath, TilePath = tilePath,
                    TotalL = fingerprint.Total.L, TotalA = fingerprint.Total.A, TotalB = fingerprint.Total.B,
                    Quadrants = [fingerprint.TopLeft, fingerprint.TopRight, fingerprint.BottomLeft, fingerprint.BottomRight]
                });

                await _colorIndex.AddPhotoAsync(userId, photoId, fingerprint, ct);
                imported++;
            }
            catch { /* skip failed photos */ }
        }

        await _db.SaveChangesAsync(ct);
        return imported;
    }
}
```

- [ ] **Step 2: Create ImportEndpoints**

```csharp
// src/Opalop.Api/Endpoints/ImportEndpoints.cs
namespace Opalop.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Opalop.Api.Services;
using Opalop.Domain.Entities;
using Opalop.Domain.Enums;
using Opalop.Infrastructure.Persistence;
using System.Security.Claims;

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/import").RequireAuthorization();

        // Google Photos OAuth flow placeholder
        // In production, Keycloak handles OAuth and provides tokens
        // These endpoints handle the photo selection and import

        group.MapPost("/google/select", async (ImportRequest request,
            GooglePhotosImporter importer, OpalopDbContext db, ClaimsPrincipal user) =>
        {
            var userId = await PhotoEndpoints.GetUserIdAsync(user, db);
            if (userId is null) return Results.Unauthorized();

            var imported = await importer.ImportPhotosAsync(userId.Value, request.PhotoUrls);

            return Results.Ok(new { imported, total = request.PhotoUrls.Count });
        });

        group.MapGet("/google/status", async (OpalopDbContext db, ClaimsPrincipal user) =>
        {
            var userId = await PhotoEndpoints.GetUserIdAsync(user, db);
            if (userId is null) return Results.Unauthorized();

            var connection = await db.SocialConnections
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Provider == SocialProvider.Google);

            return Results.Ok(new
            {
                connected = connection is not null,
                connectedAt = connection?.ConnectedAt
            });
        });
    }
}

public record ImportRequest(IReadOnlyList<string> PhotoUrls);
```

- [ ] **Step 3: Update Program.cs**

Add to services:
```csharp
builder.Services.AddHttpClient("GooglePhotos");
builder.Services.AddScoped<GooglePhotosImporter>();
```

Map endpoints:
```csharp
app.MapImportEndpoints();
```

- [ ] **Step 4: Build and commit**

```bash
dotnet build Opalop.sln
git commit -m "feat: add Google Photos import service and endpoints"
```

---

## Summary

| Task | What it produces |
|------|-----------------|
| 1 | GooglePhotosImporter service + ImportEndpoints (2 endpoints) + DI registration |
