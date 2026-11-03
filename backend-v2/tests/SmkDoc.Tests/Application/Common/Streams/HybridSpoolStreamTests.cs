using SmkDoc.Application.Common.Streams;

namespace SmkDoc.Tests.Application.Common.Streams;

public sealed class HybridSpoolStreamTests
{
    [Fact]
    public async Task WriteAndRead_BelowThreshold_RemainsInMemoryAndPreservesBytes()
    {
        // Arrange
        var payload = new byte[32 * 1024];
        Random.Shared.NextBytes(payload);

        await using var sut = new HybridSpoolStream();

        // Act
        await sut.WriteAsync(payload);
        sut.Position = 0;

        var readBuffer = new byte[payload.Length];
        await sut.ReadExactlyAsync(readBuffer);

        // Assert
        sut.IsSpilledToDisk.Should().BeFalse();
        sut.SpoolFilePath.Should().BeNull();
        sut.Length.Should().Be(payload.Length);
        readBuffer.Should().Equal(payload);

        sut.Seek(1024, SeekOrigin.Begin).Should().Be(1024);
        sut.ReadByte().Should().Be(payload[1024]);
    }

    [Fact]
    public async Task WriteAndRead_AtExactMaxThreshold_RemainsInMemoryWithoutSpilling()
    {
        // Arrange
        var payload = new byte[HybridSpoolStream.MaxInMemoryThresholdBytes];
        for (int i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i % 251);
        }

        await using var sut = new HybridSpoolStream();

        // Act
        await sut.WriteAsync(payload);
        sut.Position = 0;

        var readBuffer = new byte[payload.Length];
        await sut.ReadExactlyAsync(readBuffer);

        // Assert
        sut.IsSpilledToDisk.Should().BeFalse();
        sut.SpoolFilePath.Should().BeNull();
        readBuffer.Should().Equal(payload);
    }

    [Fact]
    public async Task WriteAndRead_AboveThreshold_SpillsToTempFileAndDeletesFileOnDisposeAsync()
    {
        // Arrange
        var payload = new byte[200 * 1024];
        for (int i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)((i * 31) % 256);
        }

        string? capturedSpoolPath;

        await using (var sut = new HybridSpoolStream())
        {
            // Act — write in mixed chunk sizes across the 64 KB threshold boundary
            await sut.WriteAsync(payload.AsMemory(0, 40_000));
            sut.IsSpilledToDisk.Should().BeFalse();

            await sut.WriteAsync(payload.AsMemory(40_000, 40_000));
            sut.IsSpilledToDisk.Should().BeTrue();
            sut.SpoolFilePath.Should().NotBeNullOrWhiteSpace();
            capturedSpoolPath = sut.SpoolFilePath;
            File.Exists(capturedSpoolPath).Should().BeTrue();

            await sut.WriteAsync(payload.AsMemory(80_000));

            // Verify random seek across pre-spill and post-spill regions
            sut.Seek(65_530, SeekOrigin.Begin).Should().Be(65_530);
            var boundarySlice = new byte[16];
            await sut.ReadExactlyAsync(boundarySlice);
            boundarySlice.Should().Equal(payload.AsSpan(65_530, 16).ToArray());

            sut.Position = 0;
            var fullRead = new byte[payload.Length];
            await sut.ReadExactlyAsync(fullRead);
            fullRead.Should().Equal(payload);
        }

        // Assert — FileOptions.DeleteOnClose deletes temp file upon stream disposal
        File.Exists(capturedSpoolPath!).Should().BeFalse();
    }

    [Fact]
    public async Task SpoolFromAsync_NonSeekableSource_RewindsToZeroAndPreservesPayload()
    {
        // Arrange
        var payload = new byte[150 * 1024];
        Random.Shared.NextBytes(payload);
        using var source = new NonSeekableReadStream(payload);

        // Act
        await using var spooled = await HybridSpoolStream.SpoolFromAsync(source);

        // Assert
        spooled.CanSeek.Should().BeTrue();
        spooled.Position.Should().Be(0);
        spooled.Length.Should().Be(payload.Length);
        spooled.IsSpilledToDisk.Should().BeTrue();

        var actual = new byte[payload.Length];
        await spooled.ReadExactlyAsync(actual);
        actual.Should().Equal(payload);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(HybridSpoolStream.MaxInMemoryThresholdBytes + 1)]
    public void Constructor_WhenThresholdOutOfRange_ThrowsArgumentOutOfRangeException(int invalidThreshold)
    {
        var act = () => new HybridSpoolStream(invalidThreshold);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private sealed class NonSeekableReadStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data, writable: false);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
