namespace SmkDoc.Application.Common.Interfaces;

public interface IStorageService
{
    Task<string> UploadAsync(string bucket, string objectName, Stream data, string contentType, CancellationToken ct = default);
    Task<Stream> DownloadAsync(string bucket, string objectName, CancellationToken ct = default);
    Task<string> GetPresignedUrlAsync(string bucket, string objectName, TimeSpan expiry, CancellationToken ct = default);
    Task DeleteAsync(string bucket, string objectName, CancellationToken ct = default);
    Task<bool> ExistsAsync(string bucket, string objectName, CancellationToken ct = default);
}
