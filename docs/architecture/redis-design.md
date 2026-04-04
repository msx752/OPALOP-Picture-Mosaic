# Redis Architecture

## Key Map

| Key Pattern | Type | Purpose | TTL |
|------------|------|---------|-----|
| `user:{uid}:colors` | Sorted Set | Color index. Score = L\* value, member = photoId | Persistent |
| `user:{uid}:photo:{photoId}` | Hash | Quadrant fingerprint (15 LAB floats) | Persistent |
| `user:{uid}:busy` | String | Processing mutex (SET NX PX) | 5 min |
| `user:{uid}:tickets` | String | Remaining usage quota | Persistent |
| `mosaic:tiles` | Stream | Tile processing job queue | MAXLEN ~10000 |
| `job:{jobId}` | Hash | Job metadata (total, completed, status, userId) | 1 hour |
| `job:{jobId}:usage` | Hash | Photo reuse counter per job | 1 hour |

## Stream Consumer Group

```
Producer (API):
  XADD mosaic:tiles * jobId {id} tileIndex {i} x {x} y {y} fingerprint {json}

Consumer Group: "workers"
  Worker-1: XREADGROUP GROUP workers worker-1 COUNT 1 > mosaic:tiles
  Worker-2: XREADGROUP GROUP workers worker-2 COUNT 1 > mosaic:tiles

Acknowledge:
  XACK mosaic:tiles workers {messageId}

Stale recovery:
  XPENDING mosaic:tiles workers - + 10
  XCLAIM mosaic:tiles workers worker-1 30000 {messageId}
```

Each tile is processed by exactly one worker. If a worker crashes, its unACKed messages are reclaimed by the StaleMessageClaimerService after 30 seconds.

## Mutex Pattern

```
Acquire: SET user:{uid}:busy 1 NX PX 300000
Release: DEL user:{uid}:busy
```

NX = only if not exists. PX 300000 = 5 minute TTL. If the process crashes, the lock auto-expires. No stuck-busy-forever bug (unlike the legacy XML-based flag).

## Color Index Operations

### Add photo (on upload)
```
ZADD user:{uid}:colors {L_value} {photoId}
HSET user:{uid}:photo:{photoId} total_L {v} total_a {v} total_b {v} q0_L ... q3_b ...
```

### Remove photo
```
ZREM user:{uid}:colors {photoId}
DEL user:{uid}:photo:{photoId}
```

### Match tile (on generation)
```
ZRANGEBYSCORE user:{uid}:colors (targetL-tol) (targetL+tol)  -> candidate IDs
For each candidate:
  HGETALL user:{uid}:photo:{id}  -> LAB values
  Compute weighted Delta-E
  Check HGET job:{jobId}:usage {id}  -> skip if >= max
Best match:
  HINCRBY job:{jobId}:usage {bestId} 1
Return {photoId, deltaE}
```

## Rebuild from PostgreSQL

If Redis is flushed or crashes, the color index can be rebuilt:

```sql
SELECT id, total_l, total_a, total_b, quadrants
FROM photos
WHERE user_id = @userId AND is_active = true
```

Then for each row: ZADD + HSET to restore the index.

## Configuration

```
redis-server --appendonly yes --maxmemory 512mb --maxmemory-policy noeviction
```

- **appendonly yes**: AOF persistence, survives restarts
- **maxmemory 512mb**: Memory limit for development
- **noeviction**: Never silently drop keys. Return errors if full (application handles gracefully)

## Memory Estimation

Per user photo:
- Sorted Set member: ~40 bytes (GUID string + score)
- Hash: 15 fields x ~20 bytes = ~300 bytes
- **Total: ~340 bytes per photo**

1000 users x 500 photos each = 500K entries = ~170MB. Well within 512MB limit.
