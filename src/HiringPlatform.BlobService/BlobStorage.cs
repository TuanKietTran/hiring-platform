using Minio;
using Minio.DataModel.Args;

namespace HiringPlatform.BlobService;

internal sealed record BlobDescriptor(
    Guid Id,
    string Name,
    string ContentType,
    long Size,
    string Purpose,
    string Url
);

internal sealed class BlobStorage(
    IMinioClient minio,
    IConfiguration configuration
) : IDisposable
{
    private const string DefaultBucket = "hirelane-blobs";
    private readonly string _bucket = configuration["S3:Bucket"] ?? DefaultBucket;
    private readonly SemaphoreSlim _initialization = new(1, 1);
    private bool _initialized;

    public async Task Put(
        Guid ownerId,
        Guid blobId,
        string name,
        string contentType,
        string purpose,
        Stream content,
        long size,
        CancellationToken ct)
    {
        await EnsureBucket(ct);
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x-amz-meta-original-name"] = Uri.EscapeDataString(name),
            ["x-amz-meta-purpose"] = purpose,
        };
        var args = new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(Key(ownerId, blobId))
            .WithStreamData(content)
            .WithObjectSize(size)
            .WithContentType(contentType)
            .WithHeaders(metadata);
        await minio.PutObjectAsync(args, ct);
    }

    public async Task<StoredBlob?> Stat(Guid ownerId, Guid blobId, CancellationToken ct)
    {
        await EnsureBucket(ct);
        try
        {
            var value = await minio.StatObjectAsync(
                new StatObjectArgs().WithBucket(_bucket).WithObject(Key(ownerId, blobId)), ct);
            var name = Metadata(value.MetaData, "original-name");
            var purpose = Metadata(value.MetaData, "purpose");
            return new StoredBlob(
                string.IsNullOrWhiteSpace(name) ? blobId.ToString() : Uri.UnescapeDataString(name),
                value.ContentType ?? "application/octet-stream",
                value.Size,
                purpose ?? "attachment");
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return null;
        }
    }

    public async Task CopyTo(
        Guid ownerId,
        Guid blobId,
        Stream destination,
        long? offset,
        long? length,
        CancellationToken ct)
    {
        var args = new GetObjectArgs()
            .WithBucket(_bucket)
            .WithObject(Key(ownerId, blobId))
            .WithCallbackStream(stream => stream.CopyToAsync(destination, ct));
        if (offset is { } start && length is { } count)
            args.WithOffsetAndLength(start, count);
        await minio.GetObjectAsync(args, ct);
    }

    public async Task Remove(Guid ownerId, Guid blobId, CancellationToken ct)
    {
        await EnsureBucket(ct);
        await minio.RemoveObjectAsync(
            new RemoveObjectArgs().WithBucket(_bucket).WithObject(Key(ownerId, blobId)), ct);
    }

    private async Task EnsureBucket(CancellationToken ct)
    {
        if (_initialized)
            return;
        await _initialization.WaitAsync(ct);
        try
        {
            if (_initialized)
                return;
            var exists = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucket), ct);
            if (!exists)
                await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucket), ct);
            _initialized = true;
        }
        finally
        {
            _initialization.Release();
        }
    }

    public void Dispose() => _initialization.Dispose();

    private static string Key(Guid ownerId, Guid blobId) => $"users/{ownerId:N}/{blobId:N}";

    private static string? Metadata(IDictionary<string, string> metadata, string key) =>
        metadata.FirstOrDefault(x => x.Key.EndsWith(key, StringComparison.OrdinalIgnoreCase)).Value;
}

internal sealed record StoredBlob(
    string Name,
    string ContentType,
    long Size,
    string Purpose
);
