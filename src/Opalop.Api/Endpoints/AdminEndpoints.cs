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

        group.MapGet("/dashboard", GetDashboardAsync);
        group.MapGet("/jobs", GetJobsAsync);
        group.MapPost("/jobs/{id}/retry", RetryJobAsync);
        group.MapGet("/users", GetUsersAsync);
        group.MapPatch("/users/{id}/quota", UpdateUserQuotaAsync);
        group.MapGet("/system/redis", GetRedisInfoAsync);
        group.MapGet("/system/health", CheckSystemHealthAsync);
    }

    private static async Task<IResult> GetDashboardAsync(
        OpalopDbContext db,
        RedisConnectionManager redis,
        CancellationToken ct)
    {
        var totalMosaics = await db.MosaicJobs.CountAsync(j => j.Status == JobStatus.Completed, ct);
        var activeJobs = await db.MosaicJobs.CountAsync(j => j.Status == JobStatus.Processing, ct);
        var totalUsers = await db.Users.CountAsync(ct);
        var totalPhotos = await db.Photos.CountAsync(ct);

        long streamLength = 0;
        try
        {
            var redisDb = redis.GetDatabase();
            streamLength = await redisDb.StreamLengthAsync("mosaic:tiles");
        }
        catch
        {
            // Redis stream may not exist yet
        }

        return Results.Ok(new
        {
            totalMosaics,
            activeJobs,
            totalUsers,
            totalPhotos,
            redisStreamLength = streamLength
        });
    }

    private static async Task<IResult> GetJobsAsync(
        OpalopDbContext db,
        string? status = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = db.MosaicJobs.AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<JobStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(j => j.Status == parsedStatus);
        }

        var totalCount = await query.CountAsync(ct);

        var jobs = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new
            {
                j.Id,
                j.UserId,
                UserEmail = j.User.Email,
                j.ResourceId,
                Status = j.Status.ToString(),
                PxFormat = j.PxFormat.Size,
                j.TotalTiles,
                j.CompletedTiles,
                j.ErrorMessage,
                j.DurationMs,
                j.CreatedAt,
                j.CompletedAt
            })
            .ToListAsync(ct);

        return Results.Ok(new { jobs, totalCount, page, pageSize });
    }

    private static async Task<IResult> RetryJobAsync(
        Guid id,
        OpalopDbContext db,
        IJobTracker jobTracker,
        IMosaicQueue mosaicQueue,
        CancellationToken ct)
    {
        var job = await db.MosaicJobs.FirstOrDefaultAsync(j => j.Id == id, ct);
        if (job is null)
            return Results.NotFound(new { error = "Job not found." });

        if (job.Status != JobStatus.Failed)
            return Results.BadRequest(new { error = "Only failed jobs can be retried." });

        job.Status = JobStatus.Processing;
        job.ErrorMessage = null;
        job.CompletedAt = null;
        job.CompletedTiles = 0;
        job.DurationMs = null;

        await db.SaveChangesAsync(ct);

        await jobTracker.InitJobAsync(job.Id, job.TotalTiles, job.UserId, job.PxFormat.Size, job.Opacity, (int)job.Style, job.CollectionId, ct);

        return Results.Ok(new { jobId = job.Id, status = job.Status.ToString() });
    }

    private static async Task<IResult> GetUsersAsync(
        OpalopDbContext db,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var totalCount = await db.Users.CountAsync(ct);

        var users = await db.Users
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.TicketBalance,
                u.IsActive,
                u.CreatedAt,
                u.LastLoginAt,
                PhotoCount = u.Photos.Count,
                JobCount = u.MosaicJobs.Count
            })
            .ToListAsync(ct);

        return Results.Ok(new { users, totalCount, page, pageSize });
    }

    private static async Task<IResult> UpdateUserQuotaAsync(
        Guid id,
        QuotaUpdate request,
        OpalopDbContext db,
        CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null)
            return Results.NotFound(new { error = "User not found." });

        user.TicketBalance = request.Tickets;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { user.Id, user.Email, user.TicketBalance });
    }

    private static async Task<IResult> GetRedisInfoAsync(
        RedisConnectionManager redis,
        CancellationToken ct)
    {
        try
        {
            var server = redis.GetServer();
            var info = await server.InfoAsync();
            var dbSize = await server.DatabaseSizeAsync();

            var infoDict = new Dictionary<string, Dictionary<string, string>>();
            foreach (var section in info)
            {
                var sectionDict = new Dictionary<string, string>();
                foreach (var entry in section)
                {
                    sectionDict[entry.Key] = entry.Value;
                }
                infoDict[section.Key] = sectionDict;
            }

            return Results.Ok(new { info = infoDict, dbSize });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Redis connection failed: {ex.Message}");
        }
    }

    private static async Task<IResult> CheckSystemHealthAsync(
        OpalopDbContext db,
        RedisConnectionManager redis,
        CancellationToken ct)
    {
        var postgresHealthy = false;
        string? postgresError = null;
        try
        {
            postgresHealthy = await db.Database.CanConnectAsync(ct);
        }
        catch (Exception ex)
        {
            postgresError = ex.Message;
        }

        var redisHealthy = false;
        string? redisError = null;
        try
        {
            var redisDb = redis.GetDatabase();
            var pong = await redisDb.PingAsync();
            redisHealthy = pong.TotalMilliseconds < 5000;
        }
        catch (Exception ex)
        {
            redisError = ex.Message;
        }

        var allHealthy = postgresHealthy && redisHealthy;

        return Results.Ok(new
        {
            status = allHealthy ? "healthy" : "degraded",
            services = new
            {
                postgresql = new { healthy = postgresHealthy, error = postgresError },
                redis = new { healthy = redisHealthy, error = redisError }
            }
        });
    }
}

public record QuotaUpdate(int Tickets);
