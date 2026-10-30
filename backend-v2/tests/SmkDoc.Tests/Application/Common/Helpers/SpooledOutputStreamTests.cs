using System.Security.Cryptography;
using SmkDoc.Application.Common.Helpers;

namespace SmkDoc.Tests.Application.Common.Helpers;

public sealed class SpooledOutputStreamTests
{
    [Fact]
    public async Task CreateAndDisposeSourceAsync_WithMultiChunkNonSeekableStream_ComputesExactHashDisposesSourceAndDeletesTempFileOnClose()
    {
        // Arrange: 200 KB payload (> 81,920-byte LOH-safe chunk size)
        var payload = new byte[200_000];
        Random.Shared.NextBytes(payload);
        var expectedSha256 = Convert.ToHexStringLower(SHA256.HashData(payload));

        var sourceStream = new TrackingNonSeekableStream(payload);
        string? underlyingTempPath;

        // Act
        await using (var spooled = await SpooledOutputStream.CreateAndDisposeSourceAsync(sourceStream))
        {
            sourceStream.IsDisposed.Should().BeTrue("upstream socket stream must be disposed immediately after spooling");
            spooled.FileSizeBytes.Should().Be(payload.Length);
            spooled.Sha256Hex.Should().Be(expectedSha256);

            var fileStream = spooled.ContentStream.Should().BeOfType<FileStream>().Subject;
            underlyingTempPath = fileStream.Name;
            File.Exists(underlyingTempPath).Should().BeTrue();

            using var readBack = new MemoryStream();
            await spooled.ContentStream.CopyToAsync(readBack);
            readBack.ToArray().Should().Equal(payload);
        }

        // Assert: FileOptions.DeleteOnClose automatically removes temporary file
        underlyingTempPath.Should().NotBeNullOrWhiteSpace();
        File.Exists(underlyingTempPath!).Should().BeFalse("DeleteOnClose must remove the temporary spool file immediately upon disposal");
    }

    private sealed class TrackingNonSeekableStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);
        public bool IsDisposed { get; private set; }

        public override bool CanRead => !IsDisposed;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, Math.Min(count, 16_384));
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var slice = buffer.Length > 16_384 ? buffer[..16_384] : buffer;
            return _inner.ReadAsync(slice, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            _inner.Dispose();
            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return _inner.DisposeAsync();
        }
    }
}
