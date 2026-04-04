# Plan 5: Admin Dashboard — Blazor SSR Admin Panel + Admin API

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the admin dashboard with Blazor SSR pages for job management, user management, worker status, and system health — plus the admin-only API endpoints that power them.

**Architecture:** Blazor SSR pages served from the Api project at `/admin/*`. Admin API endpoints at `/api/admin/*` requiring Keycloak `admin` role. Pages use server-side rendering (no WASM).

**Tech Stack:** Blazor SSR (.NET 9), Keycloak role-based auth, EF Core, StackExchange.Redis

---

## File Map

```
src/Opalop.Api/
├── Endpoints/
│   └── AdminEndpoints.cs            # /api/admin/* endpoints
├── Components/
│   ├── App.razor                     # Root Blazor layout
│   ├── Layout/
│   │   └── AdminLayout.razor         # Admin layout with nav
│   └── Pages/
│       ├── Admin/
│       │   ├── Dashboard.razor       # Overview metrics
│       │   ├── Jobs.razor            # Job list + management
│       │   ├── Users.razor           # User list + quota
│       │   └── System.razor          # Redis/PgSQL/MinIO health
```

---

### Task 1: Admin API Endpoints

**Files:**
- Create: `src/Opalop.Api/Endpoints/AdminEndpoints.cs`
- Modify: `src/Opalop.Api/Program.cs`

- [ ] **Step 1: Create AdminEndpoints**

```csharp
// src/Opalop.Api/Endpoints/AdminEndpoints.cs
namespace Opalop.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Opalop.Application.Interfaces;
using Opalop.Domain.Enums;
using Opalop.Infrastructure.Persistence;
using Opalop.Infrastructure.Redis;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization("AdminPolicy");

        group.MapGet("/dashboard", async (OpalopDbContext db, IJobTracker tracker, RedisConnectionManager redis) =>
        {
            var totalMosaics = await db.MosaicJobs.CountAsync(j => j.Status == JobStatus.Completed);
            var activeJobs = await db.MosaicJobs.CountAsync(j => j.Status == JobStatus.Processing);
            var totalUsers = await db.Users.CountAsync(u => u.IsActive);
            var totalPhotos = await db.Photos.CountAsync(p => p.IsActive);

            var redisDb = redis.GetDatabase();
            var redisInfo = await redisDb.ExecuteAsync("INFO", "memory");
            var streamInfo = await GetStreamLength(redisDb);

            return Results.Ok(new
            {
                totalMosaics, activeJobs, totalUsers, totalPhotos,
                redisMemory = redisInfo.ToString()?.Split('\n')
                    .FirstOrDefault(l => l.StartsWith("used_memory_human"))?.Split(':').LastOrDefault()?.Trim(),
                pendingTiles = streamInfo
            });
        });

        group.MapGet("/jobs", async (OpalopDbContext db, string? status, int page = 1, int pageSize = 20) =>
        {
            var query = db.MosaicJobs.Include(j => j.User).AsQueryable();
            if (status is not null && Enum.TryParse<JobStatus>(status, true, out var s))
                query = query.Where(j => j.Status == s);

            var jobs = await query
                .OrderByDescending(j => j.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(j => new
                {
                    j.Id, Status = j.Status.ToString(), j.PxFormat,
                    j.TotalTiles, j.CompletedTiles, j.DurationMs,
                    j.CreatedAt, j.CompletedAt,
                    UserEmail = j.User.Email
                })
                .ToListAsync();

            return Results.Ok(jobs);
        });

        group.MapPost("/jobs/{id}/retry", async (Guid id, OpalopDbContext db, IMosaicQueue queue, IJobTracker tracker) =>
        {
            var job = await db.MosaicJobs.FirstOrDefaultAsync(j => j.Id == id && j.Status == JobStatus.Failed);
            if (job is null) return Results.NotFound();

            job.Status = JobStatus.Processing;
            job.CompletedTiles = 0;
            job.ErrorMessage = null;
            await db.SaveChangesAsync();

            await tracker.InitJobAsync(job.Id, job.TotalTiles, job.UserId, job.PxFormat.Size);
            return Results.Ok(new { message = "Job re-queued" });
        });

        group.MapGet("/users", async (OpalopDbContext db, int page = 1, int pageSize = 20) =>
        {
            var users = await db.Users
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(u => new
                {
                    u.Id, u.Email, u.DisplayName, u.TicketBalance, u.IsActive,
                    u.CreatedAt, u.LastLoginAt,
                    PhotoCount = u.Photos.Count(p => p.IsActive),
                    JobCount = u.MosaicJobs.Count
                })
                .ToListAsync();

            return Results.Ok(users);
        });

        group.MapPatch("/users/{id}/quota", async (Guid id, QuotaUpdate update, OpalopDbContext db) =>
        {
            var user = await db.Users.FindAsync(id);
            if (user is null) return Results.NotFound();

            user.TicketBalance = update.Tickets;
            await db.SaveChangesAsync();
            return Results.Ok(new { user.Id, user.TicketBalance });
        });

        group.MapGet("/system/redis", async (RedisConnectionManager redis) =>
        {
            var db = redis.GetDatabase();
            var info = (await db.ExecuteAsync("INFO")).ToString();
            var dbSize = (long)await db.ExecuteAsync("DBSIZE");
            return Results.Ok(new { dbSize, info });
        });

        group.MapGet("/system/health", async (OpalopDbContext db, RedisConnectionManager redis) =>
        {
            var pgOk = await db.Database.CanConnectAsync();
            bool redisOk;
            try { redis.GetDatabase().Ping(); redisOk = true; } catch { redisOk = false; }

            return Results.Ok(new
            {
                postgresql = pgOk ? "healthy" : "unhealthy",
                redis = redisOk ? "healthy" : "unhealthy"
            });
        });
    }

    private static async Task<long> GetStreamLength(StackExchange.Redis.IDatabase db)
    {
        try { return await db.StreamLengthAsync("mosaic:tiles"); } catch { return 0; }
    }
}

public record QuotaUpdate(int Tickets);
```

- [ ] **Step 2: Add admin authorization policy to Program.cs**

Add to Program.cs after `AddAuthorization()`:
```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminPolicy", policy =>
        policy.RequireRole("admin"));
```

Map the admin endpoints:
```csharp
app.MapAdminEndpoints();
```

- [ ] **Step 3: Build and commit**

```bash
dotnet build Opalop.sln
git commit -m "feat: add admin API endpoints with role-based authorization"
```

---

### Task 2: Blazor SSR Admin Pages

**Files:**
- Create: `src/Opalop.Api/Components/App.razor`
- Create: `src/Opalop.Api/Components/Layout/AdminLayout.razor`
- Create: `src/Opalop.Api/Components/Pages/Admin/Dashboard.razor`
- Create: `src/Opalop.Api/Components/Pages/Admin/Jobs.razor`
- Create: `src/Opalop.Api/Components/Pages/Admin/Users.razor`
- Create: `src/Opalop.Api/Components/Pages/Admin/System.razor`
- Modify: `src/Opalop.Api/Program.cs`

- [ ] **Step 1: Add Blazor SSR to Program.cs**

```csharp
// Add to services
builder.Services.AddRazorComponents();

// Add to pipeline (before endpoints)
app.MapRazorComponents<App>();
```

- [ ] **Step 2: Create App.razor (root component)**

```razor
@* src/Opalop.Api/Components/App.razor *@
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>OPALOP Admin</title>
    <HeadOutlet />
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; background: #0f172a; color: #e2e8f0; }
        a { color: #60a5fa; text-decoration: none; }
        a:hover { text-decoration: underline; }
    </style>
</head>
<body>
    <Routes />
</body>
</html>
```

- [ ] **Step 3: Create AdminLayout.razor**

```razor
@* src/Opalop.Api/Components/Layout/AdminLayout.razor *@
@inherits LayoutComponentBase

<div style="display:flex; min-height:100vh;">
    <nav style="width:220px; background:#1e293b; padding:20px; border-right:1px solid #334155;">
        <h2 style="color:#60a5fa; margin-bottom:24px; font-size:18px;">OPALOP Admin</h2>
        <div style="display:flex; flex-direction:column; gap:8px;">
            <a href="/admin" style="padding:8px 12px; border-radius:6px; color:#94a3b8;">Dashboard</a>
            <a href="/admin/jobs" style="padding:8px 12px; border-radius:6px; color:#94a3b8;">Jobs</a>
            <a href="/admin/users" style="padding:8px 12px; border-radius:6px; color:#94a3b8;">Users</a>
            <a href="/admin/system" style="padding:8px 12px; border-radius:6px; color:#94a3b8;">System</a>
        </div>
    </nav>
    <main style="flex:1; padding:24px;">
        @Body
    </main>
</div>
```

- [ ] **Step 4: Create Dashboard.razor**

```razor
@* src/Opalop.Api/Components/Pages/Admin/Dashboard.razor *@
@page "/admin"
@layout AdminLayout
@using Microsoft.EntityFrameworkCore
@using Opalop.Domain.Enums
@using Opalop.Infrastructure.Persistence
@inject OpalopDbContext Db

<h1 style="font-size:24px; margin-bottom:24px;">Dashboard</h1>

<div style="display:grid; grid-template-columns:repeat(4,1fr); gap:16px; margin-bottom:32px;">
    <div style="background:#064e3b; border-radius:8px; padding:20px; text-align:center;">
        <div style="font-size:32px; font-weight:bold; color:#6ee7b7;">@_totalMosaics</div>
        <div style="color:#94a3b8; font-size:14px;">Total Mosaics</div>
    </div>
    <div style="background:#1c1917; border-radius:8px; padding:20px; text-align:center;">
        <div style="font-size:32px; font-weight:bold; color:#fcd34d;">@_activeJobs</div>
        <div style="color:#94a3b8; font-size:14px;">Active Jobs</div>
    </div>
    <div style="background:#312e81; border-radius:8px; padding:20px; text-align:center;">
        <div style="font-size:32px; font-weight:bold; color:#a5b4fc;">@_totalUsers</div>
        <div style="color:#94a3b8; font-size:14px;">Users</div>
    </div>
    <div style="background:#14532d; border-radius:8px; padding:20px; text-align:center;">
        <div style="font-size:32px; font-weight:bold; color:#86efac;">@_totalPhotos</div>
        <div style="color:#94a3b8; font-size:14px;">Photos</div>
    </div>
</div>

@code {
    private int _totalMosaics, _activeJobs, _totalUsers, _totalPhotos;

    protected override async Task OnInitializedAsync()
    {
        _totalMosaics = await Db.MosaicJobs.CountAsync(j => j.Status == JobStatus.Completed);
        _activeJobs = await Db.MosaicJobs.CountAsync(j => j.Status == JobStatus.Processing);
        _totalUsers = await Db.Users.CountAsync(u => u.IsActive);
        _totalPhotos = await Db.Photos.CountAsync(p => p.IsActive);
    }
}
```

- [ ] **Step 5: Create Jobs.razor, Users.razor, System.razor**

Similar pattern — server-side rendered pages querying DB directly. Jobs page shows recent jobs with status. Users page shows user list with quotas. System page shows health status.

- [ ] **Step 6: Build and commit**

```bash
dotnet build Opalop.sln
git commit -m "feat: add Blazor SSR admin dashboard with Dashboard, Jobs, Users, System pages"
```

---

## Summary

| Task | What it produces | Depends on |
|------|-----------------|------------|
| 1 | Admin API endpoints (7 endpoints) + admin auth policy | — |
| 2 | Blazor SSR pages (4 pages) + layout + root component | Task 1 |
