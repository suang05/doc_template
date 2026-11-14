namespace SmkDoc.Application.Common.Interfaces;

public interface IStorageService
{
    Task<string> UploadAsync(string bucket, string objectName, Stream data, string contentType, CancellationToken ct);
    Task<Stream> DownloadAsync(string bucket, string objectName, CancellationToken ct);
    Task<string> GetPresignedUrlAsync(string bucket, string objectName, TimeSpan expiry, CancellationToken ct);
    Task DeleteAsync(string bucket, string objectName, CancellationToken ct);
    Task<bool> ExistsAsync(string bucket, string objectName, CancellationToken ct);
    Task<IReadOnlyList<string>> ListObjectsAsync(string bucket, string prefix, CancellationToken ct);
}
