namespace Opalop.Domain.Entities;

public class PhotoCollection
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public User User { get; init; } = null!;
    public List<Photo> Photos { get; init; } = [];
}
