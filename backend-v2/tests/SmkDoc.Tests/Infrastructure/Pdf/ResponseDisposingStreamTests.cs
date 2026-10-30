using System.Net;
using SmkDoc.Infrastructure.Pdf;

namespace SmkDoc.Tests.Infrastructure.Pdf;

public sealed class ResponseDisposingStreamTests
{
    [Fact]
    public async Task DisposeAsync_DisposesBothInnerStreamAndHttpResponseMessage()
    {
        // Arrange
        var innerStream = new TrackingMemoryStream([0x25, 0x50, 0x44, 0x46]);
        var trackingContent = new TrackingHttpContent();
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = trackingContent };

        var sut = new ResponseDisposingStream(innerStream, response);

        // Act
        var buffer = new byte[4];
        int read = await sut.ReadAsync(buffer);
        await sut.DisposeAsync();

        // Assert
        read.Should().Be(4);
        innerStream.IsDisposed.Should().BeTrue();
        trackingContent.IsDisposed.Should().BeTrue("disposing HttpResponseMessage must dispose its HttpContent/socket");
    }

    [Fact]
    public void Dispose_Synchronous_DisposesBothInnerStreamAndHttpResponseMessageIdempotently()
    {
        // Arrange
        var innerStream = new TrackingMemoryStream([1, 2, 3]);
        var trackingContent = new TrackingHttpContent();
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = trackingContent };

        var sut = new ResponseDisposingStream(innerStream, response);

        // Act
        sut.Dispose();
        sut.Dispose();

        // Assert
        innerStream.DisposeCount.Should().Be(1);
        trackingContent.IsDisposed.Should().BeTrue();
    }

    private sealed class TrackingMemoryStream(byte[] bytes) : MemoryStream(bytes)
    {
        public int DisposeCount { get; private set; }
        public bool IsDisposed => DisposeCount > 0;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeCount++;
            }
            base.Dispose(disposing);
        }
    }

    private sealed class TrackingHttpContent : HttpContent
    {
        public bool IsDisposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.CompletedTask;

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
            }
            base.Dispose(disposing);
        }
    }
}
