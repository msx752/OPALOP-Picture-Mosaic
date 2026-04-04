namespace Opalop.Infrastructure.Storage;

using Minio;
using Minio.DataModel.Args;
using Opalop.Application.Interfaces;

public class MinioPhotoStorage(IMinioClient minio) : IPhotoStorage
{
    public async Task<string> UploadAsync(string bucket, string path, Stream content, string contentType, CancellationToken ct = default)
    {
        var args = new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(path)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType);

        await minio.PutObjectAsync(args, ct);
        return path;
    }

    public async Task<Stream> DownloadAsync(string bucket, string path, CancellationToken ct = default)
    {
        var ms = new MemoryStream();

        var args = new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(path)
            .WithCallbackStream(stream => stream.CopyTo(ms));

        await minio.GetObjectAsync(args, ct);
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteAsync(string bucket, string path, CancellationToken ct = default)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(bucket)
            .WithObject(path);

        await minio.RemoveObjectAsync(args, ct);
    }

    public async Task<string> GetPresignedUrlAsync(string bucket, string path, int expirySeconds = 3600, CancellationToken ct = default)
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(path)
            .WithExpiry(expirySeconds);

        return await minio.PresignedGetObjectAsync(args);
    }
}
