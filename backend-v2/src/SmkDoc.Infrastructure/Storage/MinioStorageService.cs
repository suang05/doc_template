using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Storage;

public class MinioStorageService : IStorageService
{
    private readonly IMinioClient _client;
    private readonly IMinioClient _publicClient;
    private readonly ILogger<MinioStorageService> _logger;

    public MinioStorageService(IOptions<MinioSettings> settings, ILogger<MinioStorageService> logger)
    {
        _logger = logger;
        var s = settings.Value;

        var builder = new MinioClient()
            .WithEndpoint(s.Endpoint)
            .WithCredentials(s.AccessKey, s.SecretKey);
        if (s.Secure) builder = builder.WithSSL();
        _client = builder.Build();

        // Public client for signing pre-signed URLs
        string publicEp = s.PublicEndpoint.Replace("http://", "").Replace("https://", "");
        bool publicSecure = s.PublicEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        var publicBuilder = new MinioClient()
            .WithEndpoint(publicEp)
            .WithCredentials(s.AccessKey, s.SecretKey);
        if (publicSecure) publicBuilder = publicBuilder.WithSSL();
        _publicClient = publicBuilder.Build();
    }

    public async Task<string> UploadAsync(string bucket, string objectName, Stream data, string contentType, CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(bucket, ct);

        long length = data.CanSeek ? data.Length : -1;
        var args = new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectName)
            .WithStreamData(data)
            .WithObjectSize(length)
            .WithContentType(contentType);

        await _client.PutObjectAsync(args, ct);
        return objectName;
    }

    public async Task<Stream> DownloadAsync(string bucket, string objectName, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        var args = new GetObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectName)
            .WithCallbackStream(stream => stream.CopyTo(ms));

        await _client.GetObjectAsync(args, ct);
        ms.Position = 0;
        return ms;
    }

    public async Task<string> GetPresignedUrlAsync(string bucket, string objectName, TimeSpan expiry, CancellationToken ct = default)
    {
        int expirySeconds = (int)expiry.TotalSeconds;
        var args = new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectName)
            .WithExpiry(expirySeconds);

        return await _publicClient.PresignedGetObjectAsync(args);
    }

    public async Task DeleteAsync(string bucket, string objectName, CancellationToken ct = default)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectName);
        await _client.RemoveObjectAsync(args, ct);
    }

    public async Task<bool> ExistsAsync(string bucket, string objectName, CancellationToken ct = default)
    {
        try
        {
            var args = new StatObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectName);
            await _client.StatObjectAsync(args, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task EnsureBucketExistsAsync(string bucket, CancellationToken ct)
    {
        try
        {
            var existsArgs = new BucketExistsArgs().WithBucket(bucket);
            bool found = await _client.BucketExistsAsync(existsArgs, ct);
            if (!found)
            {
                var makeArgs = new MakeBucketArgs().WithBucket(bucket);
                await _client.MakeBucketAsync(makeArgs, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not ensure bucket {Bucket} exists", bucket);
        }
    }
}
