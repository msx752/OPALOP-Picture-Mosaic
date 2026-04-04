namespace Opalop.Domain.Entities;

using Opalop.Domain.Enums;

public class SocialConnection
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public SocialProvider Provider { get; init; }
    public required string AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime ConnectedAt { get; init; } = DateTime.UtcNow;

    public User User { get; init; } = null!;
}
