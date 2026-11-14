using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;

namespace SmkDoc.Infrastructure.Storage;

public sealed partial class MinioStorageService : IStorageService, IDisposable
{
    private readonly IMinioClient _client;
    private readonly IMinioClient _publicClient;
    private readonly ILogger<MinioStorageService> _logger;
    private readonly ConcurrentDictionary<string, bool> _initializedBuckets = new(StringComparer.Ordinal);

    public MinioStorageService(IOptions<MinioSettings> settings, ILogger<MinioStorageService> logger)
    {
        _logger = logger;
        var s = settings.Value;

        _client = CreateClient(s.Endpoint, s.AccessKey, s.SecretKey, s.Secure);

        try
        {
            // Public client for signing pre-signed URLs
            string publicEp = s.PublicEndpoint
                .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
                .Replace("https://", "", StringComparison.OrdinalIgnoreCase);
            bool publicSecure = s.PublicEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            _publicClient = CreateClient(publicEp, s.AccessKey, s.SecretKey, publicSecure);
        }
        catch
        {
            _client.Dispose();
            throw;
        }
    }

    private static IMinioClient CreateClient(string endpoint, string accessKey, string secretKey, bool secure)
    {
        var client = new MinioClient();
        try
        {
            client.WithEndpoint(endpoint).WithCredentials(accessKey, secretKey);
            if (secure)
            {
                client.WithSSL();
            }

            var built = client.Build();
            if (ReferenceEquals(built, client))
            {
                return client;
            }

            client.Dispose();
            return built;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        if (!ReferenceEquals(_publicClient, _client))
        {
            _publicClient.Dispose();
        }
    }

    public async Task<string> UploadAsync(string bucket, string objectName, Stream data, string contentType, CancellationToken ct)
    {
        await EnsureBucketExistsAsync(bucket, ct);

        Stream uploadStream = data;
        HybridSpoolStream? temporarySpool = null;

        if (!data.CanSeek)
        {
            temporarySpool = await HybridSpoolStream.SpoolFromAsync(data, ct: ct);
            uploadStream = temporarySpool;
        }

        try
        {
            long length = uploadStream.Length - uploadStream.Position;
            var args = new PutObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectName)
                .WithStreamData(uploadStream)
                .WithObjectSize(length)
                .WithContentType(contentType);

            await _client.PutObjectAsync(args, ct);
            return objectName;
        }
        catch (MinioException ex)
        {
            throw new ExternalServiceException("MinIO", $"Failed to upload object '{objectName}' to bucket '{bucket}'.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException("MinIO", $"Network failure uploading object '{objectName}' to bucket '{bucket}'.", ex);
        }
        finally
        {
            if (temporarySpool is not null)
            {
                await temporarySpool.DisposeAsync();
            }
        }
    }

    public async Task<Stream> DownloadAsync(string bucket, string objectName, CancellationToken ct)
    {
        var spool = new HybridSpoolStream();
        try
        {
            var args = new GetObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectName)
                .WithCallbackStream(async (stream, cancellationToken) =>
                {
                    byte[] copyBuffer = ArrayPool<byte>.Shared.Rent(HybridSpoolStream.PooledChunkSize);
                    try
                    {
                        int bytesRead;
                        while ((bytesRead = await stream.ReadAsync(copyBuffer.AsMemory(0, HybridSpoolStream.PooledChunkSize), cancellationToken)) > 0)
                        {
                            await spool.WriteAsync(copyBuffer.AsMemory(0, bytesRead), cancellationToken);
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(copyBuffer);
                    }
                });

            await _client.GetObjectAsync(args, ct);
            spool.Position = 0;
            return spool;
        }
        catch (MinioException ex)
        {
            await spool.DisposeAsync();
            throw new ExternalServiceException("MinIO", $"Failed to download object '{objectName}' from bucket '{bucket}'.", ex);
        }
        catch (HttpRequestException ex)
        {
            await spool.DisposeAsync();
            throw new ExternalServiceException("MinIO", $"Network failure downloading object '{objectName}' from bucket '{bucket}'.", ex);
        }
        catch
        {
            await spool.DisposeAsync();
            throw;
        }
    }

    public async Task<string> GetPresignedUrlAsync(string bucket, string objectName, TimeSpan expiry, CancellationToken ct)
    {
        try
        {
            int expirySeconds = (int)expiry.TotalSeconds;
            var args = new PresignedGetObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectName)
                .WithExpiry(expirySeconds);

            return await _publicClient.PresignedGetObjectAsync(args);
        }
        catch (MinioException ex)
        {
            throw new ExternalServiceException("MinIO", $"Failed to generate pre-signed URL for '{objectName}' in bucket '{bucket}'.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException("MinIO", $"Network failure generating pre-signed URL for '{objectName}' in bucket '{bucket}'.", ex);
        }
    }

    public async Task DeleteAsync(string bucket, string objectName, CancellationToken ct)
    {
        try
        {
            var args = new RemoveObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectName);
            await _client.RemoveObjectAsync(args, ct);
        }
        catch (MinioException ex)
        {
            throw new ExternalServiceException("MinIO", $"Failed to delete object '{objectName}' from bucket '{bucket}'.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException("MinIO", $"Network failure deleting object '{objectName}' from bucket '{bucket}'.", ex);
        }
    }

    public async Task<bool> ExistsAsync(string bucket, string objectName, CancellationToken ct)
    {
        try
        {
            var args = new StatObjectArgs()
                .WithBucket(bucket)
                .WithObject(objectName);
            await _client.StatObjectAsync(args, ct);
            return true;
        }
        catch (MinioException)
        {
            return false;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> ListObjectsAsync(string bucket, string prefix, CancellationToken ct)
    {
        await EnsureBucketExistsAsync(bucket, ct);

        try
        {
            var results = new List<string>();
            var listArgs = new ListObjectsArgs()
                .WithBucket(bucket)
                .WithPrefix(prefix)
                .WithRecursive(true);

            await foreach (var item in _client.ListObjectsEnumAsync(listArgs, ct))
            {
                if (!item.IsDir && !string.IsNullOrWhiteSpace(item.Key))
                {
                    results.Add(item.Key);
                }
            }

            return results;
        }
        catch (MinioException ex)
        {
            throw new ExternalServiceException("MinIO", $"Failed to list objects in bucket '{bucket}' with prefix '{prefix}'.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceException("MinIO", $"Network failure listing objects in bucket '{bucket}' with prefix '{prefix}'.", ex);
        }
    }

    private async Task EnsureBucketExistsAsync(string bucket, CancellationToken ct)
    {
        if (_initializedBuckets.ContainsKey(bucket))
        {
            return;
        }

        try
        {
            var existsArgs = new BucketExistsArgs().WithBucket(bucket);
            bool found = await _client.BucketExistsAsync(existsArgs, ct);
            if (!found)
            {
                var makeArgs = new MakeBucketArgs().WithBucket(bucket);
                await _client.MakeBucketAsync(makeArgs, ct);
            }

            _initializedBuckets.TryAdd(bucket, true);
        }
        catch (MinioException ex)
        {
            LogEnsureBucketFailed(_logger, ex, bucket);
        }
        catch (HttpRequestException ex)
        {
            LogEnsureBucketNetworkError(_logger, ex, bucket);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Could not ensure bucket {Bucket} exists")]
    private static partial void LogEnsureBucketFailed(ILogger logger, MinioException ex, string bucket);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Could not ensure bucket {Bucket} exists due to network error")]
    private static partial void LogEnsureBucketNetworkError(ILogger logger, HttpRequestException ex, string bucket);
}
