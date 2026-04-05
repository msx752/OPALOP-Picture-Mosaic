namespace Opalop.Api.Endpoints;

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Opalop.Api.Services;
using Opalop.Application.Interfaces;
using Opalop.Infrastructure.Persistence;

public static class MosaicEndpoints
{
    private const string MosaicBucket = "mosaics";

    public static void MapMosaicEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/mosaic").RequireAuthorization();

        group.MapPost("/generate", GenerateAsync);
        group.MapGet("/{jobId}/status", GetStatusAsync);
        group.MapGet("/{jobId}/result", GetResultAsync);
        group.MapGet("/history", GetHistoryAsync);
        group.MapGet("/formats", GetFormats).AllowAnonymous();
    }

    private static async Task<IResult> GenerateAsync(
        GenerateRequest request,
        ClaimsPrincipal principal,
        OpalopDbContext db,
        MosaicOrchestrator orchestrator,
        CancellationToken ct)
    {
        var userId = await PhotoEndpoints.GetUserIdAsync(principal, db, ct);

        try
        {
            var jobId = await orchestrator.GenerateAsync(userId, request.ResourceId, request.PxFormat, request.Opacity, ct);
            return Results.Accepted($"/api/mosaic/{jobId}/status", new { jobId });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetStatusAsync(
        Guid jobId,
        IJobTracker jobTracker,
        CancellationToken ct)
    {
        var info = await jobTracker.GetJobInfoAsync(jobId, ct);
        if (info is null)
            return Results.NotFound();

        return Results.Ok(new
        {
            info.JobId,
            info.TotalTiles,
            info.CompletedTiles,
            info.Status,
            Progress = info.TotalTiles > 0
                ? Math.Round((double)info.CompletedTiles / info.TotalTiles * 100, 1)
                : 0
        });
    }

    private static async Task<IResult> GetResultAsync(
        Guid jobId,
        ClaimsPrincipal principal,
        OpalopDbContext db,
        IPhotoStorage photoStorage,
        CancellationToken ct)
    {
        var userId = await PhotoEndpoints.GetUserIdAsync(principal, db, ct);

        var job = await db.MosaicJobs
            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, ct);

        if (job is null)
            return Results.NotFound();

        if (string.IsNullOrEmpty(job.ResultPath))
            return Results.BadRequest(new { error = "Mosaic result is not yet available." });

        var url = await photoStorage.GetPresignedUrlAsync(MosaicBucket, job.ResultPath, 3600, ct);
        return Results.Ok(new { url });
    }

    private static async Task<IResult> GetHistoryAsync(
        ClaimsPrincipal principal,
        OpalopDbContext db,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = await PhotoEndpoints.GetUserIdAsync(principal, db, ct);

        var jobs = await db.MosaicJobs
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new
            {
                j.Id,
                j.ResourceId,
                Status = j.Status.ToString(),
                PxFormat = j.PxFormat.Size,
                j.TotalTiles,
                j.CompletedTiles,
                j.DurationMs,
                j.CreatedAt,
                j.CompletedAt
            })
            .ToListAsync(ct);

        var totalCount = await db.MosaicJobs.CountAsync(j => j.UserId == userId, ct);

        return Results.Ok(new { jobs, totalCount, page, pageSize });
    }

    private static IResult GetFormats()
    {
        return Results.Ok(new[] { 12, 20, 36, 48, 64, 94 });
    }
}

public record GenerateRequest(Guid ResourceId, int PxFormat, byte? Opacity = null);
