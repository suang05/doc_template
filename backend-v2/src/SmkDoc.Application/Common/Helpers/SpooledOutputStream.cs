using System.Buffers;
using System.Security.Cryptography;

namespace SmkDoc.Application.Common.Helpers;

/// <summary>
/// Zero-LOH temporary disk spooler that drains a non-seekable render stream using an ArrayPool buffer,
/// computes incremental SHA-256 and exact byte length, disposes the upstream socket stream immediately,
/// and cleans up the temporary file automatically via FileOptions.DeleteOnClose.
/// </summary>
public sealed class SpooledOutputStream : IAsyncDisposable, IDisposable
{
    public const int BufferSize = 81_920; // Strictly below 85,000-byte .NET LOH threshold

    public Stream ContentStream { get; }
    public long FileSizeBytes { get; }
    public string Sha256Hex { get; }

    private SpooledOutputStream(Stream contentStream, long fileSizeBytes, string sha256Hex)
    {
        ContentStream = contentStream;
        FileSizeBytes = fileSizeBytes;
        Sha256Hex = sha256Hex;
    }

    public static async Task<SpooledOutputStream> CreateAndDisposeSourceAsync(Stream sourceStream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);

        var tempPath = Path.Combine(Path.GetTempPath(), $"smkdoc_spool_{Guid.CreateVersion7():N}.tmp");
        var fileStream = new FileStream(
            tempPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            BufferSize,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous | FileOptions.SequentialScan);

        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long totalBytes = 0;

            await using (sourceStream)
            {
                if (sourceStream.CanSeek)
                {
                    sourceStream.Position = 0;
                }

                int bytesRead;
                while ((bytesRead = await sourceStream.ReadAsync(rentedBuffer.AsMemory(0, BufferSize), ct)) > 0)
                {
                    hasher.AppendData(rentedBuffer, 0, bytesRead);
                    await fileStream.WriteAsync(rentedBuffer.AsMemory(0, bytesRead), ct);
                    totalBytes += bytesRead;
                }
            }

            await fileStream.FlushAsync(ct);
            fileStream.Position = 0;

            var sha256Hex = Convert.ToHexStringLower(hasher.GetHashAndReset());
            return new SpooledOutputStream(fileStream, totalBytes, sha256Hex);
        }
        catch
        {
            await fileStream.DisposeAsync();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    public void Dispose() => ContentStream.Dispose();

    public ValueTask DisposeAsync() => ContentStream.DisposeAsync();
}
