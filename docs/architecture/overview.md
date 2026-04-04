# Architecture Overview

## System Design

OPALOP uses a **Modular Monolith** architecture with 6 .NET projects in a single solution. The system separates into two deployable units (API + Worker) sharing domain and infrastructure code.

```
                    +-----------+
                    |  Client   |
                    +-----+-----+
                          |
                    HTTP / WebSocket
                          |
                    +-----v-----+       +----------+
                    |  Keycloak  |<----->|  API     |
                    | (OAuth2)   |       | Server   |
                    +-----------+       +----+-----+
                                             |
                          +------------------+------------------+
                          |                  |                  |
                    +-----v-----+      +-----v-----+     +-----v-----+
                    | PostgreSQL |      |   Redis    |     |   MinIO   |
                    | (data)     |      | (index,    |     | (blobs)   |
                    |            |      |  queue,    |     |           |
                    +-----^-----+      |  tracker)  |     +-----^-----+
                          |            +-----+-----+           |
                          |                  |                  |
                    +-----+------------------+------------------+-----+
                    |                Worker 1                          |
                    |                Worker 2                          |
                    |                Worker N                          |
                    +-------------------------------------------------+
```

## Layer Dependencies

```
Domain          (zero dependencies)
  ^
  |--- Application (interfaces)
  |--- Mosaic.Engine (SkiaSharp)
  |
  v
Infrastructure  (EF Core, Redis, MinIO)
  ^
  |
  +--- Api     (HTTP, SignalR, Blazor)
  +--- Worker  (BackgroundService)
```

Inner layers never reference outer layers. Domain is pure C#. Application defines contracts. Infrastructure implements them. Api and Worker consume everything.

## Data Storage Strategy

| Data Type | Primary Store | Cache/Index |
|-----------|--------------|-------------|
| Users, jobs, photos (metadata) | PostgreSQL | -- |
| Color fingerprints | PostgreSQL (backup) | Redis Sorted Set + Hash |
| Job state & progress | Redis Hash | -- |
| Processing mutex | Redis String (NX+TTL) | -- |
| Tile job queue | Redis Stream | -- |
| Photo binaries | MinIO (S3) | -- |
| Mosaic results | MinIO (S3) | -- |

**Principle:** Redis is cache, PostgreSQL is truth. If Redis flushes, color index rebuilds from PostgreSQL.

## Key Design Decisions

### 1. Redis Streams over Message Brokers

We use Redis Streams with Consumer Groups instead of RabbitMQ/Kafka:
- One fewer infrastructure dependency
- Consumer Groups provide at-least-once delivery
- XCLAIM handles worker failures
- XACK tracks completion
- Already using Redis for color index and job tracking

### 2. CIE L\*a\*b\* over RGB

The legacy system used packed RGB integers where Red channel was weighted 65,536x more than Blue. L\*a\*b\* is perceptually uniform -- Delta-E correlates with human vision. Pre-filtering by L\* (lightness) using Sorted Set gives O(log N + M) lookup.

### 3. Tile-based over Strip-based Parallelism

Legacy: image split into 8 strips, one per server. If one strip is harder, others wait.
New: image split into N tiles (e.g., 64), distributed via Stream. Workers pull tiles as they finish -- natural load balancing. Scale by adding workers, no code change.

### 4. SkiaSharp over System.Drawing

System.Drawing/GDI+ is Windows-only and unsupported on .NET 5+/Linux. SkiaSharp is cross-platform, actively maintained, runs on Alpine Docker images.

### 5. Modular Monolith over Microservices

Same scaling capability (worker containers scale independently) but simpler operations:
- Single solution, single CI pipeline
- Shared domain model
- 6 Docker containers vs 9+
- Module boundaries via project references -- extractable to microservices if needed later
