using System.Buffers;
using System.Security.Cryptography;

namespace SmkDoc.Application.Common.Streams;

/// <summary>
/// Single-pass render output spooler backed by <see cref="HybridSpoolStream"/>.
/// Drains an upstream render stream with a 64 KB pooled buffer (<see cref="HybridSpoolStream.PooledChunkSize"/>,
/// strictly below the 85,000-byte .NET LOH threshold) while computing incremental SHA-256 and exact byte length,
/// disposes the upstream socket stream immediately, and keeps small payloads (&lt;= 64 KB) in pooled RAM before
/// spilling larger payloads to a <see cref="FileOptions.DeleteOnClose"/> temporary file.
/// </summary>
public sealed class SpooledRenderOutput : IAsyncDisposable, IDisposable
{
    public const int BufferSize = HybridSpoolStream.PooledChunkSize;

    private readonly HybridSpoolStream _spoolStream;

    public Stream ContentStream => _spoolStream;
    public long FileSizeBytes { get; }
    public string Sha256Hex { get; }

    private SpooledRenderOutput(HybridSpoolStream spoolStream, long fileSizeBytes, string sha256Hex)
    {
        _spoolStream = spoolStream;
        FileSizeBytes = fileSizeBytes;
        Sha256Hex = sha256Hex;
    }

    public static async Task<SpooledRenderOutput> CreateAndDisposeSourceAsync(Stream sourceStream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);

        var spool = new HybridSpoolStream();
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
                    await spool.WriteAsync(rentedBuffer.AsMemory(0, bytesRead), ct);
                    totalBytes += bytesRead;
                }
            }

            await spool.FlushAsync(ct);
            spool.Position = 0;

            var sha256Hex = Convert.ToHexStringLower(hasher.GetHashAndReset());
            return new SpooledRenderOutput(spool, totalBytes, sha256Hex);
        }
        catch
        {
            await spool.DisposeAsync();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
        }
    }

    public void Dispose() => _spoolStream.Dispose();

    public ValueTask DisposeAsync() => _spoolStream.DisposeAsync();
}
