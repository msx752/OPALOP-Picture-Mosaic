namespace Opalop.Domain.Entities;

public class User
{
    public Guid Id { get; init; }
    public required string KeycloakId { get; init; }
    public required string Email { get; set; }
    public string? DisplayName { get; set; }
    public int TicketBalance { get; set; } = 100;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    public List<Photo> Photos { get; init; } = [];
    public List<Resource> Resources { get; init; } = [];
    public List<MosaicJob> MosaicJobs { get; init; } = [];
    public List<SocialConnection> SocialConnections { get; init; } = [];
}
