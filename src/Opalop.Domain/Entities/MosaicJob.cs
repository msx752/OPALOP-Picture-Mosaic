namespace Opalop.Domain.Entities;

using Opalop.Domain.Enums;
using Opalop.Domain.ValueObjects;

public class MosaicJob
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public Guid ResourceId { get; init; }
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public PixFormat PxFormat { get; init; } = PixFormat.Default;
    public byte Opacity { get; init; } = 128;
    public MosaicStyle Style { get; init; } = MosaicStyle.Overlay;
    public Guid? CollectionId { get; init; }
    public int TotalTiles { get; set; }
    public int CompletedTiles { get; set; }
    public string? ResultPath { get; set; }
    public string? ErrorMessage { get; set; }
    public long? DurationMs { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public User User { get; init; } = null!;
    public Resource Resource { get; init; } = null!;
}
