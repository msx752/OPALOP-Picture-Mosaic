namespace Opalop.Domain.Entities;

using Opalop.Domain.Enums;
using Opalop.Domain.ValueObjects;

public class Photo
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public required string Filename { get; init; }
    public PhotoSource Source { get; init; }
    public required string StoragePath { get; init; }
    public string? TilePath { get; set; }
    public float TotalL { get; set; }
    public float TotalA { get; set; }
    public float TotalB { get; set; }
    public List<QuadrantLab> Quadrants { get; init; } = [];
    public bool IsActive { get; set; } = true;
    public DateTime UploadedAt { get; init; } = DateTime.UtcNow;

    public User User { get; init; } = null!;
}
