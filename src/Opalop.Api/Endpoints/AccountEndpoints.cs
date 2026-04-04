namespace Opalop.Api.Endpoints;

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Opalop.Infrastructure.Persistence;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/account").RequireAuthorization();

        group.MapGet("/me", GetMeAsync);
        group.MapGet("/quota", GetQuotaAsync);
    }

    private static async Task<IResult> GetMeAsync(
        ClaimsPrincipal principal,
        OpalopDbContext db,
        CancellationToken ct)
    {
        var userId = await PhotoEndpoints.GetUserIdAsync(principal, db, ct);

        var user = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.DisplayName,
                u.CreatedAt,
                u.LastLoginAt,
                PhotoCount = u.Photos.Count(p => p.IsActive)
            })
            .FirstAsync(ct);

        return Results.Ok(user);
    }

    private static async Task<IResult> GetQuotaAsync(
        ClaimsPrincipal principal,
        OpalopDbContext db,
        CancellationToken ct)
    {
        var userId = await PhotoEndpoints.GetUserIdAsync(principal, db, ct);

        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);

        return Results.Ok(new
        {
            ticketBalance = user.TicketBalance,
            isActive = user.IsActive
        });
    }
}
