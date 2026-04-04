namespace Opalop.Api.Endpoints;

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Opalop.Api.Services;
using Opalop.Domain.Enums;
using Opalop.Infrastructure.Persistence;

public record ImportRequest(IReadOnlyList<string> PhotoUrls);

public static class ImportEndpoints
{
    public static void MapImportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/import").RequireAuthorization();

        group.MapPost("/google/select", SelectAsync);
        group.MapGet("/google/status", StatusAsync);
    }

    private static async Task<IResult> SelectAsync(
        ImportRequest request,
        ClaimsPrincipal principal,
        OpalopDbContext db,
        GooglePhotosImporter importer,
        CancellationToken ct)
    {
        if (request.PhotoUrls is null || request.PhotoUrls.Count == 0)
            return Results.BadRequest("No photo URLs provided.");

        var userId = await PhotoEndpoints.GetUserIdAsync(principal, db, ct);
        var count = await importer.ImportAsync(userId, request.PhotoUrls, ct);

        return Results.Ok(new { imported = count, total = request.PhotoUrls.Count });
    }

    private static async Task<IResult> StatusAsync(
        ClaimsPrincipal principal,
        OpalopDbContext db,
        CancellationToken ct)
    {
        var userId = await PhotoEndpoints.GetUserIdAsync(principal, db, ct);

        var connection = await db.SocialConnections
            .FirstOrDefaultAsync(sc => sc.UserId == userId && sc.Provider == SocialProvider.Google, ct);

        var isConnected = connection is not null;
        return Results.Ok(new { isConnected });
    }
}
