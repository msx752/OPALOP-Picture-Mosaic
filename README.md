# OPALOP Picture Mosaic

A distributed image mosaic generation system that converts a source photograph into a high-resolution mosaic composed of thousands of smaller photos. Each tile region is replaced by a mini photo that best matches its color using CIE L\*a\*b\* perceptual color matching.

![Mosaic Example 20x20](https://raw.githubusercontent.com/MSAlih1/OPALOP-Picture-Mosaic/master/20x20_test.jpg)

## Architecture

**Modular Monolith** built on .NET 9 with Redis-based parallel processing:

```
Client
  |
  v
[API Server] ---- Keycloak (OAuth2/OIDC)
  |
  |-- POST /api/photos/upload --> MinIO + Redis Color Index
  |-- POST /api/mosaic/generate --> Tile Grid Analysis --> Redis Stream fan-out
  |
  v
[Redis Stream: mosaic:tiles]
  |
  +--> [Worker 1] --+
  +--> [Worker 2] --+--> Color Match (Lua) --> SkiaSharp Composite --> MinIO
  +--> [Worker N] --+
  |
  v
[MosaicAssembler] --> Final JPEG --> MinIO --> SignalR notification
```

Workers scale dynamically: `docker compose up --scale opalop-worker=8`

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 9 |
| API | ASP.NET Minimal API + SignalR |
| Image Processing | SkiaSharp (cross-platform) |
| Color Matching | CIE L\*a\*b\* + Weighted Delta-E |
| Job Queue | Redis Streams + Consumer Groups |
| Color Index | Redis Sorted Set + Lua scripting |
| Database | PostgreSQL + EF Core 9 |
| Blob Storage | MinIO (S3-compatible) |
| Auth | Keycloak (OAuth2/OIDC) |
| Admin UI | Blazor SSR |
| Containers | Docker Compose |

## Project Structure

```
src/
  Opalop.Domain/              Pure C# entities, value objects, enums
  Opalop.Application/         Use case interfaces (IColorIndex, IMosaicQueue, etc.)
  Opalop.Infrastructure/      EF Core, Redis, MinIO implementations
  Opalop.Mosaic.Engine/       SkiaSharp image processing, LAB color space, SmartRotate
  Opalop.Api/                 Minimal API endpoints, SignalR hub, Blazor admin dashboard
  Opalop.Worker/              Redis Stream consumer, tile processor, mosaic assembler

tests/
  Opalop.Domain.Tests/
  Opalop.Mosaic.Engine.Tests/
  Opalop.Infrastructure.Tests/

infra/
  keycloak/                   Realm export with test users
  redis/lua/                  Color matching Lua scripts
  minio/                      Bucket initialization
```

## Quick Start

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### Run with Docker Compose (full stack)

```bash
docker compose up
```

This starts all 6 services:
- **opalop-api** at http://localhost:5000
- **opalop-worker** (2 replicas by default)
- **redis** at localhost:6379
- **postgresql** at localhost:5432
- **minio** at http://localhost:9001 (console)
- **keycloak** at http://localhost:8080

### Run locally (development)

```bash
# Start infrastructure services
docker compose up -d redis postgresql minio keycloak

# Apply database migrations
dotnet ef database update -p src/Opalop.Infrastructure -s src/Opalop.Api

# Run API and Worker in separate terminals
dotnet run --project src/Opalop.Api
dotnet run --project src/Opalop.Worker
```

### Scale workers

```bash
docker compose up --scale opalop-worker=8
```

## Access Points

| Service | URL |
|---------|-----|
| API | http://localhost:5000 |
| Swagger UI | http://localhost:5000/swagger |
| Admin Dashboard | http://localhost:5000/admin |
| SignalR Hub | ws://localhost:5000/hubs/mosaic |
| Keycloak Admin | http://localhost:8080 (admin/admin) |
| MinIO Console | http://localhost:9001 (minioadmin/minioadmin) |

### Test Users (Keycloak)

| Username | Password | Roles |
|----------|----------|-------|
| testuser | test123 | user |
| admin | admin123 | admin, user |

## API Endpoints

### Photos (JWT required)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/photos/upload` | Upload mini photo (auto-crop, fingerprint, index) |
| GET | `/api/photos` | List library photos (paginated) |
| GET | `/api/photos/{id}/thumbnail` | Pre-signed MinIO URL |
| DELETE | `/api/photos/{id}` | Remove from library |

### Resources (JWT required)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/resources/upload` | Upload source photo (max 12MB) |
| GET | `/api/resources` | List uploaded resources |

### Mosaic Generation (JWT required)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/mosaic/generate` | Start mosaic job (returns 202 + jobId) |
| GET | `/api/mosaic/{jobId}/status` | Job progress (tiles completed/total) |
| GET | `/api/mosaic/{jobId}/result` | Download completed mosaic |
| GET | `/api/mosaic/history` | Past mosaics (paginated) |
| GET | `/api/mosaic/formats` | Available tile sizes: 12, 20, 36, 48, 64, 94 |

### Import (JWT required)

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/import/google/select` | Import Google Photos by URL |
| GET | `/api/import/google/status` | Google connection status |

### Account (JWT required)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/account/me` | User profile + photo count |
| GET | `/api/account/quota` | Remaining tickets |

### Admin (requires `admin` role)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/admin/dashboard` | Aggregate stats |
| GET | `/api/admin/jobs` | All jobs (filterable) |
| POST | `/api/admin/jobs/{id}/retry` | Re-queue failed job |
| GET | `/api/admin/users` | User list with quotas |
| PATCH | `/api/admin/users/{id}/quota` | Update user quota |
| GET | `/api/admin/system/redis` | Redis metrics |
| GET | `/api/admin/system/health` | System health check |

### System

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/health` | Health check (public) |

## How Mosaic Generation Works

1. **Upload mini photos** to build your color library (or import from Google Photos)
2. **Upload a source photo** (the image to be converted into a mosaic)
3. **Generate** with a chosen tile size (e.g., 94x94 pixels)

The system then:
1. Applies Gaussian blur to the source for stable color sampling
2. Splits the source into a grid of tiles (e.g., 752x752 image / 94px tiles = 64 tiles)
3. For each tile, computes a **CIE L\*a\*b\* color fingerprint** (5 values: 4 quadrants + total average)
4. Fans out all tiles to a Redis Stream for parallel processing
5. Each worker:
   - Finds the best matching photo using **weighted Delta-E** (perceptual color distance)
   - Applies **SmartRotate** to align the photo's brightest quadrant with the tile
   - Composites the photo onto the tile position with alpha blending
6. When all tiles are done, the **MosaicAssembler** combines them into the final JPEG
7. Real-time progress is pushed via **SignalR**

### Color Matching: CIE L\*a\*b\*

Unlike the legacy system's packed RGB integer matching (where Red channel dominated 65,536x over Blue), the new system uses CIE L\*a\*b\* color space:

- **L\*** = lightness (0-100) -- used for pre-filtering via Redis Sorted Set
- **a\*** = green-red axis
- **b\*** = blue-yellow axis
- **Delta-E** = Euclidean distance in LAB space, correlates with human perception

Matching formula:
```
score = 0.4 * DeltaE(total) + 0.15 * DeltaE(q0) + 0.15 * DeltaE(q1) + 0.15 * DeltaE(q2) + 0.15 * DeltaE(q3)
```

## Running Tests

```bash
# All tests
dotnet test

# Unit tests only (no Docker needed)
dotnet test tests/Opalop.Domain.Tests
dotnet test tests/Opalop.Mosaic.Engine.Tests

# Integration tests (requires Docker Desktop for Testcontainers)
dotnet test tests/Opalop.Infrastructure.Tests
```

## Design Documents

- [Architecture Design Spec](docs/superpowers/specs/2026-04-04-opalop-net9-migration-design.md) -- Full architectural specification
- [Plan 1: Foundation](docs/superpowers/plans/2026-04-04-plan1-foundation.md)
- [Plan 2: Mosaic Engine](docs/superpowers/plans/2026-04-04-plan2-mosaic-engine.md)
- [Plan 3: Redis Infrastructure](docs/superpowers/plans/2026-04-04-plan3-redis-infrastructure.md)
- [Plan 4: API & Worker](docs/superpowers/plans/2026-04-04-plan4-api-worker.md)
- [Plan 5: Admin Dashboard](docs/superpowers/plans/2026-04-04-plan5-admin-dashboard.md)
- [Plan 6: Social Import](docs/superpowers/plans/2026-04-04-plan6-social-import.md)

## Legacy System

The `MainAPIServer/` and `ClusterServer/` directories contain the original .NET Framework 4.6 implementation with SOAP-based cluster communication. These are preserved for reference but are not part of the new system.

## License

Apache License, Version 2.0
