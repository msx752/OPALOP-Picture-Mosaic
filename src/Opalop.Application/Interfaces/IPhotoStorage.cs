namespace Opalop.Application.Interfaces;

public interface IPhotoStorage
{
    Task<string> UploadAsync(string bucket, string path, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string bucket, string path, CancellationToken ct = default);
    Task DeleteAsync(string bucket, string path, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string bucket, string path, int expirySeconds = 3600, CancellationToken ct = default);
}
