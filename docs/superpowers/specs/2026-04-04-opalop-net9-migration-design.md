# OPALOP Picture Mosaic — .NET 9 Migration & Architectural Redesign

**Date:** 2026-04-04
**Status:** Approved
**Scope:** Full rewrite from .NET Framework 4.6 to .NET 9 with Redis-based processing architecture

---

## 1. Executive Summary

OPALOP is a distributed image mosaic generation system that converts a source photograph into a high-resolution mosaic composed of thousands of smaller photos. The current system runs on .NET Framework 4.6 with SOAP-based cluster communication, file-system state, and GDI+ image processing — all deprecated or unsupported in modern .NET.

This design document specifies a complete architectural redesign targeting .NET 9 with:
- **Redis** as the processing engine (color indexing, job queue, progress tracking, caching)
- **PostgreSQL** as the persistent data store (users, job history, photo metadata)
- **MinIO** for S3-compatible binary storage (photos, mosaics)
- **Keycloak** for OAuth2/OIDC authentication
- **SkiaSharp** replacing System.Drawing/GDI+ for cross-platform image processing
- **CIE L\*a\*b\*** color space replacing packed RGB for perceptually accurate color matching
- **Docker Compose** for local development with scalable workers

---

## 2. Architecture Decision: Modular Monolith

**Chosen:** Modular Monolith (Option A) over Microservices (B) and Single Process (C).

**Rationale:**
- Same parallel processing capability as microservices via Redis Stream Consumer Groups
- Worker scaling: `docker compose up --scale opalop-worker=N` — no code changes
- 6 containers vs 9+ (microservices) — lower operational complexity
- Single solution, single CI pipeline, shared domain model
- Module boundaries enforced via project references — extractable to microservices later if needed
- Single process (C) rejected: CPU-bound mosaic processing blocks API, in-memory queue not shareable

---

## 3. Solution Structure

```
src/
├── Opalop.Domain/           # Entities, Value Objects, Domain Events
│                              User, MosaicJob, PhotoLibrary, TileResult, ColorFingerprint
│                              Dependency: NONE — pure C# classes
│
├── Opalop.Application/      # Use cases, Interface definitions, DTOs
│                              IColorIndex, IPhotoStorage, IMosaicQueue, IJobTracker
│                              CreateMosaicJob, UploadPhoto, ImportFromGoogle, GetJobProgress
│                              Dependency: Domain only
│
├── Opalop.Infrastructure/   # External system implementations
│                              EF Core DbContext (PostgreSQL), Redis client, MinIO client, Keycloak
│                              RedisColorIndex : IColorIndex
│                              MinioPhotoStorage : IPhotoStorage
│                              RedisMosaicQueue : IMosaicQueue
│                              Dependency: Domain + Application
│
├── Opalop.Mosaic.Engine/    # Pure image processing — the project's heart
│                              SkiaSharp: Quard analysis, CIE L*a*b* conversion, SmartRotate
│                              TileAnalyzer, ColorMatcher, TileCompositor, MosaicAssembler
│                              Dependency: Domain only
│
├── Opalop.Api/              # HTTP endpoints + Admin Dashboard
│                              Minimal API, SignalR Hub, Blazor SSR Admin
│                              Keycloak middleware, health checks
│                              Dependency: Application + Infrastructure
│                              Dockerfile: .NET 9 Alpine
│
└── Opalop.Worker/           # Redis Stream consumer — background processor
                               TileProcessorService : BackgroundService
                               JobCompletionService — assembly trigger
                               Dependency: Application + Infrastructure + Mosaic.Engine
                               Dockerfile: .NET 9 Alpine

tests/
├── Opalop.Domain.Tests/
├── Opalop.Mosaic.Engine.Tests/   # Most critical: color matching accuracy
├── Opalop.Application.Tests/
└── Opalop.Api.Tests/
```

**Dependency direction:** Outer layers depend on inner. Domain depends on nothing. Api and Worker are outermost.

---

## 4. Technology Stack

| Layer | Technology | Replaces |
|-------|-----------|----------|
| Runtime | .NET 9 | .NET Framework 4.6 |
| API | Minimal API + SignalR | ASP.NET Web API 2.3 |
| Image Processing | SkiaSharp | System.Drawing / GDI+ |
| Color Space | CIE L\*a\*b\* + ΔE | Packed RGB integer |
| Job Queue | Redis Streams + Consumer Groups | SOAP WebMethods × 8 |
| Color Index | Redis Sorted Set + Lua | In-memory LINQ + disk I/O per request |
| Auth | Keycloak (OAuth2/OIDC) | Auth0 + custom JWT handler |
| Database | PostgreSQL + EF Core 9 | XML files (UserInfo.xml) |
| Blob Storage | MinIO (S3-compatible) | Local filesystem (UserFolders/) |
| Admin UI | Blazor SSR | None (API only) |
| Containers | Docker Compose | Azure Web Apps × 9 |

### Key NuGet Packages

| Project | Package | Purpose |
|---------|---------|---------|
| Mosaic.Engine | SkiaSharp | Cross-platform image processing |
| Infrastructure | StackExchange.Redis | Redis client (Stream, Sorted Set, Lua, Pub/Sub) |
| Infrastructure | Npgsql.EntityFrameworkCore.PostgreSQL | EF Core PostgreSQL provider |
| Infrastructure | Minio | MinIO .NET SDK |
| Api | Microsoft.AspNetCore.Authentication.JwtBearer | Keycloak JWT validation |
| Api | Microsoft.AspNetCore.SignalR | Real-time progress |
| Api | Swashbuckle.AspNetCore | OpenAPI / Swagger |
| Tests | xUnit + Testcontainers | Integration tests with real Redis/PgSQL |

---

## 5. Color Matching Engine

### Current System Problems

The existing system uses a packed 24-bit RGB integer `(R << 16) | (G << 8) | B` as the color matching key. This creates severe perceptual inaccuracy:

- **Red channel bias:** R changes are weighted 65,536× more than B changes
- **Perceptually similar colors can be numerically distant:** Two similar blues (100,149,237) and (70,130,220) differ by ~2M in packed value — outside tolerance
- **Perceptually different colors can be numerically close:** Green (0,100,0) and Blue (0,0,106) differ by only ~25K — within tolerance (wrong match)

Additional bugs found in the current algorithm:
1. `Quard()` TotalAvg excludes quadrant 3 (SağAlt/bottom-right) — average is ~75% correct
2. `Transparnt()` multiplies R,G,B channels by opacity (not just Alpha) — darkens all mosaics
3. `RotateImage()` uses 182° instead of 180° — permanent 2° tilt on rotated tiles
4. Mini photo resize guard uses AND instead of OR — wrong size when pxFormat ≠ 94
5. `GetImageAVGRgb()` divides by `width²` instead of `width × height`
6. Edge repair in `RotateImage()` uses `Width` for Y coordinate instead of `Height`

### New System: CIE L\*a\*b\* + Quadrant Fingerprint

**Color space:** CIE L\*a\*b\* — designed to be perceptually uniform. ΔE (Euclidean distance in LAB) correlates with human perception:
- ΔE < 1: imperceptible
- ΔE 1-2: noticeable on close inspection
- ΔE 2-10: noticeable at first glance
- ΔE > 10: clearly different color

**Fingerprint per photo:** 5 × 3 = 15 float values:
- 4 quadrants (top-left, top-right, bottom-left, bottom-right) × (L, a, b)
- 1 total average × (L, a, b)

**Matching algorithm:**
1. **Pre-filter:** Redis Sorted Set with L\* as score → `ZRANGEBYSCORE` for brightness-similar candidates
2. **Rank:** For each candidate, compute weighted ΔE:
   ```
   score = 0.4×ΔE(total) + 0.15×ΔE(q0) + 0.15×ΔE(q1) + 0.15×ΔE(q2) + 0.15×ΔE(q3)
   ```
3. **Select:** Lowest score wins. Usage counter prevents excessive repetition.
4. **Fallback:** If no match within ΔE ≤ 50 after expansion, fill tile with its own average color.

All matching logic runs inside a single Lua script — zero network round-trips.

### SmartRotate (Preserved, Improved)

The quadrant-based orientation concept is preserved. In the new system, rotation is determined by quadrant LAB matching rather than ARGB abs comparison. SkiaSharp `RotateFlip()` replaces GDI+ rotation — no 182° hack needed.

### Image Processing Pipeline (New)

```
Upload → MinIO (original) → SkiaSharp: square crop (aspect-preserving) → resize to pxFormat
       → Quard(): 4-quadrant RGB sample → RGB-to-LAB conversion → ColorFingerprint
       → Redis: ZADD (L* score) + HSET (15 LAB floats)
       → PostgreSQL: INSERT (metadata + LAB backup)

Generate → Source photo from MinIO → SkiaSharp: Blur(5,5) → tile grid → LAB fingerprint per tile
         → XADD 64 tiles to Redis Stream → Workers process in parallel
         → Each tile: Lua match → MinIO read photo → SmartRotate → alpha composite (A channel only)
         → XACK + HINCRBY progress → last tile triggers MosaicAssembler
         → Final JPEG → MinIO → SignalR notification
```

**Key improvements over current pipeline:**
- Color index computed once at upload (not every request)
- Alpha compositing only modifies A channel (not R,G,B) — no darkening
- Tile-based parallelism (not strip-based) — better load balancing
- Dynamic worker count (not hardcoded 8)
- Aspect-preserving crop (not squish) for mini photos

---

## 6. Redis Architecture

### Key Map

| Key Pattern | Type | Purpose | TTL |
|------------|------|---------|-----|
| `user:{uid}:colors` | Sorted Set | Color index — score=L*, member=photoId | Persistent |
| `user:{uid}:photo:{photoId}` | Hash | 5 quadrant × 3 LAB values + filename | Persistent |
| `user:{uid}:busy` | String | Concurrent processing mutex | 5 min (crash-safe) |
| `user:{uid}:tickets` | String (int) | Remaining usage quota | Persistent |
| `mosaic:tiles` | Stream | Job queue — tile processing tasks | MAXLEN ~10000 |
| `job:{jobId}` | Hash | Job metadata — total, completed, status, userId | 1 hour |
| `job:{jobId}:usage` | Hash | Photo reuse counter — photoId → count | 1 hour |
| `job:{jobId}:progress` | Pub/Sub | Real-time progress → SignalR bridge | — |

### Lua Scripts

**match_tile.lua** — Atomic color matching:
1. `ZRANGEBYSCORE` on user's color index with L* tolerance
2. For each candidate: `HGETALL` quadrant LAB values
3. Compute weighted ΔE score
4. Check usage counter (`HINCRBY` if below max)
5. Return best match photoId + rotation info

**claim_stale.lua** — Worker failover:
1. `XPENDING` to find unACKed messages older than 30s
2. `XCLAIM` to reassign to requesting worker
3. Increment retry counter; move to dead letter after 3 failures

### Configuration

```
redis-server --appendonly yes --maxmemory 512mb --maxmemory-policy noeviction
```

`noeviction` ensures color index keys are never silently dropped. If memory is full, Redis returns errors (which the application handles) rather than evicting data.

### Redis ↔ PostgreSQL Sync

Redis is cache, PostgreSQL is truth. On Redis flush/crash:
```sql
SELECT id, total_l, total_a, total_b, quadrants
FROM photos WHERE user_id = ? AND is_active = true
```
→ Rebuild Sorted Set + Hash entries per user.

Ticket balance: periodic reconciliation every 5 minutes between Redis and PostgreSQL.

---

## 7. Data Flow

### Flow 1: Photo Upload & Indexing

```
POST /api/photos/upload
→ Api: JWT validate → file validate (size, format)
→ MinIO: store original at photos/{userId}/originals/{photoId}.jpg
→ Engine: SkiaSharp square crop → resize → Quard() → RGB-to-LAB → ColorFingerprint
→ MinIO: store tile at photos/{userId}/tiles/{photoId}.jpg
→ Redis: ZADD user:{uid}:colors + HSET user:{uid}:photo:{photoId}
→ PostgreSQL: INSERT INTO photos (metadata + LAB backup)
→ Response: 201 + photoId
```

### Flow 2: Mosaic Generation

```
POST /api/mosaic/generate { resourceId, pxFormat }
→ Api: SET user:{uid}:busy NX PX 300000 (atomic mutex with TTL)
→ Api: DECR user:{uid}:tickets (atomic quota)
→ Api: Load resource from MinIO → Blur(5,5) → tile grid → LAB fingerprint per tile
→ Api: INSERT mosaic_jobs → HSET job:{jobId} metadata
→ Api: XADD mosaic:tiles × N tiles (fan-out)
→ Response: 202 + jobId

Worker (×N):
→ XREADGROUP GROUP workers BLOCK 5000 mosaic:tiles
→ Per tile: EVALSHA match_tile.lua → MinIO read photo → SmartRotate → composite
→ MinIO: write tile result → XACK → HINCRBY job:{jobId} completed
→ PUBLISH job:{jobId}:progress → Api subscriber → SignalR → client

Last tile worker:
→ HGET job:{jobId} completed == total_tiles → trigger assembly
→ MinIO: read all tiles → MosaicAssembler → final JPEG → MinIO
→ HSET job:{jobId} status=completed → PostgreSQL UPDATE
→ DEL user:{uid}:busy → cleanup
```

### Error Recovery

| Scenario | Mechanism |
|----------|-----------|
| Worker crash | XPENDING + XCLAIM after 30s → another worker takes over |
| Redis crash | AOF persistence; rebuild color index from PostgreSQL |
| Quota error | DECR returns < 0 → INCR rollback → HTTP 402 |
| Job failure | Busy flag TTL auto-expires (5 min); ticket refunded via INCR |
| No color match | ΔE expansion phases (10 → 25 → 50); ultimate fallback: solid color fill |
| Dead letter | 3 failed retries → move to `mosaic:deadletter` stream → admin retry |

---

## 8. API Specification

### User API (JWT required, `User` role)

**Photo Management:**
| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/photos/upload` | Upload mini photo → crop → index |
| POST | `/api/photos/upload/batch` | Batch upload (max 50) |
| GET | `/api/photos` | List library photos (paginated) |
| GET | `/api/photos/{id}/thumbnail` | Pre-signed MinIO URL |
| DELETE | `/api/photos/{id}` | Delete from MinIO + Redis + PgSQL |
| GET | `/api/photos/stats` | Library statistics |

**Social Media Import:**
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/import/google/auth` | Google Photos OAuth redirect |
| GET | `/api/import/google/callback` | OAuth callback → save token |
| GET | `/api/import/google/photos` | List Google Photos |
| POST | `/api/import/google/select` | Import selected → library |

**Mosaic Generation:**
| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/mosaic/generate` | Start mosaic job → 202 + jobId |
| GET | `/api/mosaic/{jobId}/status` | Job status (polling fallback) |
| GET | `/api/mosaic/{jobId}/result` | Completed mosaic pre-signed URL |
| GET | `/api/mosaic/history` | User's mosaic history (paginated) |
| GET | `/api/mosaic/formats` | Available tile sizes |

**Resource Photos (mosaic target):**
| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/resources/upload` | Upload source photo |
| GET | `/api/resources` | List uploaded resources |

**Account:**
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/account/me` | Current user profile + quota |
| GET | `/api/account/quota` | Remaining tickets |

**System:**
| Method | Route | Description |
|--------|-------|-------------|
| GET | `/health` | Health check (public) |

### Admin API (JWT required, `admin` role)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/api/admin/jobs` | All jobs (filter: status, user, date) |
| POST | `/api/admin/jobs/{id}/retry` | Re-queue failed job |
| GET | `/api/admin/workers` | Worker statuses (consumer group info) |
| GET | `/api/admin/users` | User list (Keycloak + PgSQL merge) |
| PATCH | `/api/admin/users/{id}/quota` | Update user quota |
| GET | `/api/admin/system/redis` | Redis INFO, MEMORY, XINFO |
| GET | `/api/admin/system/health` | Detailed health: all services + metrics |

### SignalR Hub: `/hubs/mosaic`

**Server → Client:**
- `JobProgress` — { jobId, completed, total, percent }
- `JobCompleted` — { jobId, resultUrl, duration }
- `JobFailed` — { jobId, error, failedTiles }
- `ImportProgress` — { source, imported, total }

**Client → Server:**
- `SubscribeToJob(jobId)` — join job group for progress
- `UnsubscribeFromJob(jobId)` — leave job group

---

## 9. Admin Dashboard (Blazor SSR)

Route: `/admin` — requires Keycloak `admin` role.

### Pages

**Dashboard:**
- Real-time job queue (active/queued/completed)
- Worker status (online/offline/busy)
- Last 24h mosaic production chart
- Redis memory & stream lag metrics

**Job Management:**
- Active/completed/failed job list with filters
- Job detail: tile progress, duration, error log
- Retry failed jobs
- Dead letter queue view / retry

**User Management:**
- User list (synced from Keycloak)
- Quota view / edit
- Library size & mosaic history per user
- Activate / deactivate users

**System:**
- Redis: memory usage, key count, stream info
- PostgreSQL: connection pool stats
- MinIO: disk usage, bucket stats
- Health check statuses

---

## 10. PostgreSQL Schema

### Tables

**users:**
| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | gen_random_uuid() |
| keycloak_id | varchar UNIQUE | External identity |
| email | varchar | |
| display_name | varchar | |
| ticket_balance | int DEFAULT 100 | Usage quota |
| is_active | bool DEFAULT true | |
| created_at | timestamptz | |
| last_login_at | timestamptz | |

**photos:**
| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| user_id | uuid FK → users | |
| filename | varchar | |
| source | varchar | 'upload' or 'google' |
| storage_path | varchar | MinIO path (original) |
| tile_path | varchar | MinIO path (square tile) |
| total_l, total_a, total_b | real | CIE L\*a\*b\* total average (Redis backup) |
| quadrants | jsonb | [{l,a,b} × 4] quadrant fingerprints |
| is_active | bool DEFAULT true | |
| uploaded_at | timestamptz | |

**resources:**
| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| user_id | uuid FK → users | |
| filename | varchar | |
| storage_path | varchar | MinIO path |
| width, height | int | Original dimensions |
| file_size_bytes | bigint | |
| uploaded_at | timestamptz | |

**mosaic_jobs:**
| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| user_id | uuid FK → users | |
| resource_id | uuid FK → resources | |
| status | varchar | queued, processing, completed, failed |
| px_format | int | Tile size (12, 20, 36, 48, 64, 94) |
| total_tiles | int | |
| completed_tiles | int DEFAULT 0 | |
| result_path | varchar NULL | MinIO path for completed mosaic |
| error_message | text NULL | |
| duration_ms | int NULL | |
| created_at | timestamptz | |
| completed_at | timestamptz NULL | |

**social_connections:**
| Column | Type | Notes |
|--------|------|-------|
| id | uuid PK | |
| user_id | uuid FK → users | |
| provider | varchar | 'google' or 'instagram' |
| access_token | text | Encrypted (DataProtection) |
| refresh_token | text | Encrypted |
| expires_at | timestamptz | |
| connected_at | timestamptz | |

### Indexes

```sql
CREATE INDEX ix_photos_user_active ON photos (user_id) WHERE is_active = true;
CREATE INDEX ix_photos_user_l ON photos (user_id, total_l) WHERE is_active = true;
CREATE INDEX ix_jobs_user_status ON mosaic_jobs (user_id, status);
CREATE INDEX ix_jobs_created ON mosaic_jobs (created_at DESC);
CREATE INDEX ix_resources_user ON resources (user_id);
CREATE UNIQUE INDEX ix_users_keycloak ON users (keycloak_id);
CREATE UNIQUE INDEX ix_social_user_provider ON social_connections (user_id, provider);
```

### Conventions
- Snake_case table/column names (`UseSnakeCaseNamingConvention()`)
- UUID primary keys (DB-generated)
- `timestamptz` for all dates (UTC)
- No soft delete — real DELETE (GDPR compliance)
- Quadrants stored as `jsonb` — EF Core 9 owned entity mapping

---

## 11. Docker Compose

### Services

| Service | Image | Ports | Scale |
|---------|-------|-------|-------|
| opalop-api | Custom .NET 9 Alpine | 5000 | 1 |
| opalop-worker | Custom .NET 9 Alpine | headless | N (`--scale`) |
| redis | redis:7-alpine | 6379 | 1 |
| postgresql | postgres:16-alpine | 5432 | 1 |
| minio | minio/minio:latest | 9000, 9001 | 1 |
| keycloak | keycloak:24.0 | 8080 | 1 |

### Volumes
- `redis-data` — AOF persistence
- `pgdata` — PostgreSQL data
- `minio-data` — Object storage

### Redis Configuration
```
redis-server --appendonly yes --maxmemory 512mb --maxmemory-policy noeviction
```

### Worker Configuration
- `Worker__ConsumerGroup=workers`
- `Worker__StreamKey=mosaic:tiles`
- `Worker__ClaimTimeout=30000` (30s before XCLAIM stale messages)
- Default replicas: 2

### Dockerfile Strategy
Multi-stage Alpine build. SkiaSharp native deps: `libskiasharp`, `fontconfig`, `freetype`. Final image ~120MB.

---

## 12. Repository Structure

```
OPALOP-Picture-Mosaic/
├── src/
│   ├── Opalop.Domain/
│   ├── Opalop.Application/
│   ├── Opalop.Infrastructure/
│   ├── Opalop.Mosaic.Engine/
│   ├── Opalop.Api/                 (+ Dockerfile)
│   └── Opalop.Worker/              (+ Dockerfile)
├── tests/
│   ├── Opalop.Domain.Tests/
│   ├── Opalop.Mosaic.Engine.Tests/
│   ├── Opalop.Application.Tests/
│   └── Opalop.Api.Tests/
├── infra/
│   ├── keycloak/realm-export.json
│   ├── redis/lua/
│   │   ├── match_tile.lua
│   │   ├── claim_stale.lua
│   │   └── rebuild_index.lua
│   └── minio/init-buckets.sh
├── docker-compose.yml
├── docker-compose.override.yml
├── Opalop.sln
├── .editorconfig
├── Directory.Build.props
└── Directory.Packages.props
```

---

## 13. Development Workflow

### First-time Setup
```bash
git clone && cd OPALOP-Picture-Mosaic
docker compose up -d redis postgresql minio keycloak
dotnet ef database update -p src/Opalop.Infrastructure
dotnet run --project src/Opalop.Api
dotnet run --project src/Opalop.Worker
```

Or fully containerized: `docker compose up`

### Access Points
| Service | URL |
|---------|-----|
| API | http://localhost:5000 |
| Swagger | http://localhost:5000/swagger |
| Admin Dashboard | http://localhost:5000/admin |
| SignalR | ws://localhost:5000/hubs/mosaic |
| Keycloak Admin | http://localhost:8080 |
| MinIO Console | http://localhost:9001 |

---

## 14. Migration from Current System

### What is Preserved (Improved)
- Gaussian Blur(5,5) pre-processing — reduces noise for stable color averaging
- 4-quadrant spatial fingerprint — concept correct, moved to CIE L\*a\*b\*
- SmartRotate — concept preserved, SkiaSharp RotateFlip (no 182° hack)
- pxFormat-aligned trim/crop — required for grid alignment, decoupled from worker count
- ArithmeticBlend HDR effect — offered as optional toggle, default off

### What is Fixed
- Color matching: CIE L\*a\*b\* + ΔE replaces biased packed RGB
- TotalAvg: all 4 quadrants included (Q3 bug fixed)
- Alpha compositing: only A channel modified (no R,G,B darkening)
- Rotation: SkiaSharp RotateFlip — exact angles, no pixel shift
- Mini photo resize: dynamic pxFormat-based, aspect-preserving crop
- Busy flag: SET NX PX with TTL — crash-safe (no permanent lock)
- Ticket system: atomic DECR (no XML race condition)
- Progress: Pub/Sub + SignalR (no Thread.Sleep busy-wait)

### What is Removed
- System.Drawing / GDI+ — replaced by SkiaSharp
- SOAP/ASMX WebMethods — replaced by Redis Streams
- XML file-based state — replaced by PostgreSQL + Redis
- Auth0 — replaced by Keycloak
- Hardcoded ComputerNumber=8 — dynamic worker scaling
- Instagram API (deprecated) — replaced by Google Photos + direct upload
- File-system storage — replaced by MinIO
