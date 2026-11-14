using System.Buffers;

namespace SmkDoc.Application.Common.Streams;

/// <summary>
/// Zero-LOH seekable stream spooler.
/// Payloads up to <see cref="MaxInMemoryThresholdBytes"/> (64 KB) stay in a pooled <see cref="ArrayPool{T}"/> buffer
/// strictly below the 85,000-byte .NET Large Object Heap (LOH) threshold.
/// Payloads exceeding the threshold seamlessly spill into an OS page-cache-backed temporary <see cref="FileStream"/>
/// configured with <see cref="FileOptions.DeleteOnClose"/> for deterministic cleanup upon disposal.
/// </summary>
public sealed class HybridSpoolStream : Stream
{
    /// <summary>
    /// Maximum and default in-memory threshold (65,536 bytes = 64 KB = 2^16),
    /// ensuring <see cref="ArrayPool{T}.Shared"/> bucket rentals stay strictly below the 85,000-byte LOH boundary.
    /// </summary>
    public const int MaxInMemoryThresholdBytes = 65_536;
    public const int DefaultInMemoryThresholdBytes = MaxInMemoryThresholdBytes;
    public const int PooledChunkSize = 65_536;

    private readonly int _thresholdBytes;
    private byte[]? _pooledBuffer;
    private int _inMemoryLength;
    private long _inMemoryPosition;
    private FileStream? _fileStream;
    private bool _disposed;

    public HybridSpoolStream(int inMemoryThresholdBytes = DefaultInMemoryThresholdBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inMemoryThresholdBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(inMemoryThresholdBytes, MaxInMemoryThresholdBytes);
        _thresholdBytes = inMemoryThresholdBytes;
    }

    /// <summary>
    /// Indicates whether the stream has spilled over the in-memory threshold into a temporary OS file.
    /// </summary>
    public bool IsSpilledToDisk => _fileStream is not null;

    /// <summary>
    /// Backing temporary file path when <see cref="IsSpilledToDisk"/> is <see langword="true"/>; otherwise <see langword="null"/>.
    /// </summary>
    public string? SpoolFilePath => _fileStream?.Name;

    public override bool CanRead => !_disposed;
    public override bool CanSeek => !_disposed;
    public override bool CanWrite => !_disposed;

    public override long Length
    {
        get
        {
            EnsureNotDisposed();
            return _fileStream?.Length ?? _inMemoryLength;
        }
    }

    public override long Position
    {
        get
        {
            EnsureNotDisposed();
            return _fileStream?.Position ?? _inMemoryPosition;
        }
        set
        {
            EnsureNotDisposed();
            ArgumentOutOfRangeException.ThrowIfNegative(value);

            if (_fileStream is not null)
            {
                _fileStream.Position = value;
            }
            else
            {
                _inMemoryPosition = value;
            }
        }
    }

    /// <summary>
    /// Drains <paramref name="source"/> into a new <see cref="HybridSpoolStream"/> using a pooled buffer and rewinds <see cref="Position"/> to 0.
    /// Does not dispose <paramref name="source"/>.
    /// </summary>
    public static async Task<HybridSpoolStream> SpoolFromAsync(
        Stream source,
        int inMemoryThresholdBytes = DefaultInMemoryThresholdBytes,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var spool = new HybridSpoolStream(inMemoryThresholdBytes);
        byte[] copyBuffer = ArrayPool<byte>.Shared.Rent(PooledChunkSize);
        try
        {
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(copyBuffer.AsMemory(0, PooledChunkSize), ct)) > 0)
            {
                await spool.WriteAsync(copyBuffer.AsMemory(0, bytesRead), ct);
            }

            spool.Position = 0;
            return spool;
        }
        catch
        {
            await spool.DisposeAsync();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(copyBuffer);
        }
    }

    /// <summary>
    /// Writes the UTF-8 encoded representation of <paramref name="text"/> into a new <see cref="HybridSpoolStream"/>
    /// using pooled chunked encoding, avoiding unbounded byte[] LOH allocations.
    /// Rewinds <see cref="Position"/> to 0 upon completion.
    /// </summary>
    public static async Task<HybridSpoolStream> CreateFromUtf8StringAsync(
        string? text,
        int inMemoryThresholdBytes = DefaultInMemoryThresholdBytes,
        CancellationToken ct = default)
    {
        var spool = new HybridSpoolStream(inMemoryThresholdBytes);
        if (string.IsNullOrEmpty(text))
        {
            return spool;
        }

        var encoder = System.Text.Encoding.UTF8.GetEncoder();
        var chars = text.AsMemory();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(PooledChunkSize);

        try
        {
            while (!chars.IsEmpty)
            {
                encoder.Convert(
                    chars.Span,
                    buffer.AsSpan(0, PooledChunkSize),
                    flush: chars.Length <= PooledChunkSize,
                    out int charsUsed,
                    out int bytesUsed,
                    out bool completed);

                if (bytesUsed > 0)
                {
                    await spool.WriteAsync(buffer.AsMemory(0, bytesUsed), ct);
                }

                chars = chars[charsUsed..];
                if (completed) break;
            }

            spool.Position = 0;
            return spool;
        }
        catch
        {
            await spool.DisposeAsync();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public override void Flush()
    {
        EnsureNotDisposed();
        _fileStream?.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        EnsureNotDisposed();
        return _fileStream is not null ? _fileStream.FlushAsync(cancellationToken) : Task.CompletedTask;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        EnsureNotDisposed();

        if (_fileStream is not null)
        {
            return _fileStream.Read(buffer);
        }

        if (buffer.IsEmpty || _pooledBuffer is null || _inMemoryPosition >= _inMemoryLength)
        {
            return 0;
        }

        int available = (int)Math.Min(buffer.Length, _inMemoryLength - _inMemoryPosition);
        _pooledBuffer.AsSpan((int)_inMemoryPosition, available).CopyTo(buffer);
        _inMemoryPosition += available;
        return available;
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (_fileStream is not null)
        {
            return _fileStream.ReadAsync(buffer, cancellationToken);
        }

        int bytesRead = Read(buffer.Span);
        return ValueTask.FromResult(bytesRead);
    }

    public override int ReadByte()
    {
        Span<byte> single = stackalloc byte[1];
        return Read(single) == 1 ? single[0] : -1;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        EnsureNotDisposed();

        if (_fileStream is not null)
        {
            return _fileStream.Seek(offset, origin);
        }

        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _inMemoryPosition + offset,
            SeekOrigin.End => _inMemoryLength + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (target < 0)
        {
            throw new IOException("An attempt was made to move the position before the beginning of the stream.");
        }

        _inMemoryPosition = target;
        return target;
    }

    public override void SetLength(long value)
    {
        EnsureNotDisposed();
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        if (_fileStream is not null)
        {
            _fileStream.SetLength(value);
            return;
        }

        if (value > _thresholdBytes)
        {
            SpillToDisk();
            _fileStream!.SetLength(value);
            return;
        }

        int newLength = (int)value;
        if (newLength > _inMemoryLength)
        {
            EnsurePooledBuffer();
            _pooledBuffer!.AsSpan(_inMemoryLength, newLength - _inMemoryLength).Clear();
        }

        _inMemoryLength = newLength;
        if (_inMemoryPosition > newLength)
        {
            _inMemoryPosition = newLength;
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        Write(buffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        EnsureNotDisposed();

        if (buffer.IsEmpty)
        {
            return;
        }

        if (_fileStream is not null)
        {
            _fileStream.Write(buffer);
            return;
        }

        long requiredEnd = _inMemoryPosition + buffer.Length;
        if (requiredEnd > _thresholdBytes)
        {
            SpillToDisk();
            _fileStream!.Write(buffer);
            return;
        }

        WriteInMemory(buffer);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBufferArguments(buffer, offset, count);
        return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        if (buffer.IsEmpty)
        {
            return;
        }

        if (_fileStream is not null)
        {
            await _fileStream.WriteAsync(buffer, cancellationToken);
            return;
        }

        long requiredEnd = _inMemoryPosition + buffer.Length;
        if (requiredEnd > _thresholdBytes)
        {
            await SpillToDiskAsync(cancellationToken);
            await _fileStream!.WriteAsync(buffer, cancellationToken);
            return;
        }

        WriteInMemory(buffer.Span);
    }

    public override void WriteByte(byte value)
    {
        ReadOnlySpan<byte> single = [value];
        Write(single);
    }

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        if (disposing)
        {
            ReturnPooledBuffer();
            _fileStream?.Dispose();
            _fileStream = null;
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        _disposed = true;
        ReturnPooledBuffer();

        if (_fileStream is not null)
        {
            await _fileStream.DisposeAsync();
            _fileStream = null;
        }

        await base.DisposeAsync();
    }

    private void WriteInMemory(ReadOnlySpan<byte> buffer)
    {
        EnsurePooledBuffer();

        if (_inMemoryPosition > _inMemoryLength)
        {
            _pooledBuffer!.AsSpan(_inMemoryLength, (int)_inMemoryPosition - _inMemoryLength).Clear();
        }

        buffer.CopyTo(_pooledBuffer!.AsSpan((int)_inMemoryPosition, buffer.Length));
        _inMemoryPosition += buffer.Length;

        if (_inMemoryPosition > _inMemoryLength)
        {
            _inMemoryLength = (int)_inMemoryPosition;
        }
    }

    private void EnsurePooledBuffer()
    {
        if (_pooledBuffer is null && _thresholdBytes > 0)
        {
            _pooledBuffer = ArrayPool<byte>.Shared.Rent(_thresholdBytes);
        }
    }

    private void SpillToDisk()
    {
        var fileStream = CreateTempFileStream();
        try
        {
            if (_inMemoryLength > 0 && _pooledBuffer is not null)
            {
                fileStream.Write(_pooledBuffer.AsSpan(0, _inMemoryLength));
            }

            fileStream.Position = _inMemoryPosition;
            _fileStream = fileStream;
            ReturnPooledBuffer();
        }
        catch
        {
            fileStream.Dispose();
            throw;
        }
    }

    private async ValueTask SpillToDiskAsync(CancellationToken ct)
    {
        var fileStream = CreateTempFileStream();
        try
        {
            if (_inMemoryLength > 0 && _pooledBuffer is not null)
            {
                await fileStream.WriteAsync(_pooledBuffer.AsMemory(0, _inMemoryLength), ct);
            }

            fileStream.Position = _inMemoryPosition;
            _fileStream = fileStream;
            ReturnPooledBuffer();
        }
        catch
        {
            await fileStream.DisposeAsync();
            throw;
        }
    }

    private static FileStream CreateTempFileStream()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"smkdoc_hybrid_{Guid.CreateVersion7():N}.tmp");
        return new FileStream(
            tempPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            PooledChunkSize,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous | FileOptions.RandomAccess);
    }

    private void ReturnPooledBuffer()
    {
        var rented = Interlocked.Exchange(ref _pooledBuffer, null);
        if (rented is not null)
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
