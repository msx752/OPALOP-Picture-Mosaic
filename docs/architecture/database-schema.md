# PostgreSQL Database Schema

## Tables

### users

| Column | Type | Constraints |
|--------|------|-------------|
| id | uuid | PK, default gen_random_uuid() |
| keycloak_id | varchar | UNIQUE, NOT NULL |
| email | varchar | NOT NULL |
| display_name | varchar | |
| ticket_balance | int | DEFAULT 100 |
| is_active | bool | DEFAULT true |
| created_at | timestamptz | |
| last_login_at | timestamptz | |

### photos

| Column | Type | Constraints |
|--------|------|-------------|
| id | uuid | PK, default gen_random_uuid() |
| user_id | uuid | FK -> users(id) |
| filename | varchar | NOT NULL |
| source | varchar | 'Upload' or 'Google' |
| storage_path | varchar | NOT NULL (MinIO original path) |
| tile_path | varchar | (MinIO square tile path) |
| total_l | real | CIE L\* total average |
| total_a | real | CIE a\* total average |
| total_b | real | CIE b\* total average |
| quadrants | jsonb | [{L,A,B} x 4] quadrant fingerprints |
| is_active | bool | DEFAULT true |
| uploaded_at | timestamptz | |

### resources

| Column | Type | Constraints |
|--------|------|-------------|
| id | uuid | PK, default gen_random_uuid() |
| user_id | uuid | FK -> users(id) |
| filename | varchar | NOT NULL |
| storage_path | varchar | NOT NULL |
| width | int | |
| height | int | |
| file_size_bytes | bigint | |
| uploaded_at | timestamptz | |

### mosaic_jobs

| Column | Type | Constraints |
|--------|------|-------------|
| id | uuid | PK, default gen_random_uuid() |
| user_id | uuid | FK -> users(id) |
| resource_id | uuid | FK -> resources(id) |
| status | varchar | 'Queued', 'Processing', 'Completed', 'Failed' |
| px_format | int | Tile size (12, 20, 36, 48, 64, 94) |
| total_tiles | int | |
| completed_tiles | int | DEFAULT 0 |
| result_path | varchar | MinIO path for completed mosaic |
| error_message | text | |
| duration_ms | int | |
| created_at | timestamptz | |
| completed_at | timestamptz | |

### social_connections

| Column | Type | Constraints |
|--------|------|-------------|
| id | uuid | PK, default gen_random_uuid() |
| user_id | uuid | FK -> users(id) |
| provider | varchar | 'Google' or 'Instagram' |
| access_token | text | NOT NULL (encrypted at rest) |
| refresh_token | text | |
| expires_at | timestamptz | |
| connected_at | timestamptz | |

## Indexes

```sql
CREATE INDEX ix_photos_user_active ON photos (user_id) WHERE is_active = true;
CREATE INDEX ix_photos_user_l ON photos (user_id, total_l) WHERE is_active = true;
CREATE INDEX ix_jobs_user_status ON mosaic_jobs (user_id, status);
CREATE INDEX ix_jobs_created ON mosaic_jobs (created_at DESC);
CREATE INDEX ix_resources_user ON resources (user_id);
CREATE UNIQUE INDEX ix_users_keycloak ON users (keycloak_id);
CREATE UNIQUE INDEX ix_social_user_provider ON social_connections (user_id, provider);
```

## Relationships

```
users 1--N photos
users 1--N resources
users 1--N mosaic_jobs
users 1--N social_connections
resources 1--N mosaic_jobs
```

## Conventions

- Snake_case naming (EFCore.NamingConventions)
- UUID primary keys (DB-generated)
- All timestamps in UTC (timestamptz)
- Enums stored as strings
- No soft delete (real DELETE for GDPR)
- Quadrants stored as jsonb for flexible querying

## EF Core

Migrations are in `src/Opalop.Infrastructure/Persistence/Migrations/`.

```bash
# Generate migration
dotnet ef migrations add <Name> -p src/Opalop.Infrastructure -s src/Opalop.Api

# Apply migration
dotnet ef database update -p src/Opalop.Infrastructure -s src/Opalop.Api
```
