namespace Opalop.Application.Interfaces;

using Opalop.Domain.ValueObjects;

public interface IColorIndex
{
    Task AddPhotoAsync(Guid userId, Guid photoId, ColorFingerprint fingerprint, CancellationToken ct = default);
    Task RemovePhotoAsync(Guid userId, Guid photoId, CancellationToken ct = default);
    Task<ColorMatchResult?> FindBestMatchAsync(Guid userId, ColorFingerprint target, Guid jobId,
        int maxUsagePerPhoto = 5, int tileRow = -1, int tileCol = -1, int minDistance = 3,
        CancellationToken ct = default);
    Task RebuildIndexAsync(Guid userId, IEnumerable<(Guid PhotoId, ColorFingerprint Fingerprint)> photos, CancellationToken ct = default);
}

public record ColorMatchResult(Guid PhotoId, float DeltaE, ColorFingerprint Fingerprint);
