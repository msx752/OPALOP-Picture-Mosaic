# Docker & Deployment

## Services

| Service | Image | Ports | Replicas |
|---------|-------|-------|----------|
| opalop-api | Custom (.NET 9 Alpine) | 5000:8080 | 1 |
| opalop-worker | Custom (.NET 9 Alpine) | none (headless) | N (default 2) |
| redis | redis:7-alpine | 6379 | 1 |
| postgresql | postgres:16-alpine | 5432 | 1 |
| minio | minio/minio:latest | 9000, 9001 | 1 |
| minio-init | minio/mc:latest | none (init job) | 1 (exits) |
| keycloak | keycloak:24.0 | 8080 | 1 |

## Scaling Workers

```bash
# Default (2 workers)
docker compose up

# Scale to 8 workers
docker compose up --scale opalop-worker=8

# Scale down to 1
docker compose up --scale opalop-worker=1
```

No code change needed. Workers auto-join the Redis Stream consumer group.

## Docker Compose Files

- `docker-compose.yml` — Full stack definition
- `docker-compose.override.yml` — Dev environment variables (Development mode)

## Volumes

| Volume | Purpose |
|--------|---------|
| redis-data | Redis AOF persistence |
| pgdata | PostgreSQL data directory |
| minio-data | Object storage files |

## MinIO Buckets

Created automatically by `minio-init`:
- `photos` — User mini photos (originals + tiles)
- `resources` — Source photos (mosaic targets)
- `mosaics` — Generated mosaic results + intermediate tiles

## Keycloak Realm

Auto-imported from `infra/keycloak/realm-export.json`:
- Realm: `opalop`
- Client: `opalop-api` (public, direct access grants)
- Roles: `user`, `admin`
- Test users: `testuser` (user), `admin` (admin+user)

## Health Checks

- PostgreSQL: `pg_isready` (Docker healthcheck)
- API: `GET /health` (checks Redis + PostgreSQL connectivity)
- MinIO: `GET /minio/health/live`

## Dockerfile Strategy

Multi-stage Alpine build:

```
Stage 1 (sdk:9.0-alpine): restore + publish
Stage 2 (aspnet:9.0-alpine): runtime only (~120MB)
```

SkiaSharp native deps: `libskiasharp`, `fontconfig`, `freetype` (installed in runtime stage when needed).

## Environment Variables

### API

| Variable | Default | Description |
|----------|---------|-------------|
| ConnectionStrings__PostgreSQL | (required) | PostgreSQL connection string |
| ConnectionStrings__Redis | (required) | Redis connection string |
| MinIO__Endpoint | (required) | MinIO server endpoint |
| MinIO__AccessKey | (required) | MinIO access key |
| MinIO__SecretKey | (required) | MinIO secret key |
| MinIO__UseSSL | false | Enable SSL for MinIO |
| Keycloak__Authority | (required) | Keycloak realm URL |
| Keycloak__Audience | opalop-api | JWT audience |
| ASPNETCORE_ENVIRONMENT | Production | Environment name |

### Worker

Same as API minus Keycloak, plus:

| Variable | Default | Description |
|----------|---------|-------------|
| Worker__ConsumerGroup | workers | Redis Stream consumer group |
| Worker__StreamKey | mosaic:tiles | Redis Stream key |
| Worker__ClaimTimeout | 30000 | Stale message timeout (ms) |
