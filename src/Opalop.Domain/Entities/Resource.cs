namespace Opalop.Domain.Entities;

public class Resource
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public required string Filename { get; init; }
    public required string StoragePath { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public long FileSizeBytes { get; init; }
    public DateTime UploadedAt { get; init; } = DateTime.UtcNow;

    public User User { get; init; } = null!;
    public List<MosaicJob> MosaicJobs { get; init; } = [];
}
