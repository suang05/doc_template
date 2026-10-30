namespace SmkDoc.Infrastructure.Pdf;

/// <summary>
/// Stream decorator that guarantees disposal of the owning HttpResponseMessage
/// and underlying socket stream when the caller disposes the stream.
/// </summary>
public sealed class ResponseDisposingStream(Stream innerStream, HttpResponseMessage response) : Stream
{
    private int _disposed;

    public override bool CanRead => innerStream.CanRead;
    public override bool CanSeek => innerStream.CanSeek;
    public override bool CanWrite => false;
    public override long Length => innerStream.Length;

    public override long Position
    {
        get => innerStream.Position;
        set => innerStream.Position = value;
    }

    public override void Flush() => innerStream.Flush();
    public override Task FlushAsync(CancellationToken ct) => innerStream.FlushAsync(ct);
    public override int Read(byte[] buffer, int offset, int count) => innerStream.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => innerStream.Read(buffer);
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
        innerStream.ReadAsync(buffer, offset, count, ct);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
        innerStream.ReadAsync(buffer, ct);
    public override long Seek(long offset, SeekOrigin origin) => innerStream.Seek(offset, origin);
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0 && disposing)
        {
            try
            {
                innerStream.Dispose();
            }
            finally
            {
                response.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            try
            {
                await innerStream.DisposeAsync();
            }
            finally
            {
                response.Dispose();
            }
        }

        await base.DisposeAsync();
    }
}
