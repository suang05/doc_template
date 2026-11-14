using Microsoft.Extensions.Logging.Abstractions;
using SmkDoc.Infrastructure.Imaging;

namespace SmkDoc.Tests.Infrastructure.Imaging;

public sealed class ImageOptimizerServiceTests
{
    private readonly ImageOptimizerService _sut = new(NullLogger<ImageOptimizerService>.Instance);

    [Fact]
    public void Optimize_WithNullBytes_ReturnsEmpty()
    {
        var result = _sut.Optimize(null!, 0, 0);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Optimize_WithSmallImage_ReturnsSameReference()
    {
        // Under MinSizeForOptimization (50 KB) → no processing
        var tiny = new byte[1024];
        var result = _sut.Optimize(tiny, 0, 0);
        result.Should().BeSameAs(tiny);
    }

    [Fact]
    public void Optimize_WithInvalidImageData_ReturnsSameBytes()
    {
        // 60 KB of deterministic non-image bytes
        var garbage = new byte[60 * 1024];
        for (int i = 0; i < garbage.Length; i++)
        {
            garbage[i] = (byte)((i * 73 + 19) % 256);
        }
        var result = _sut.Optimize(garbage, 0, 0);
        result.Should().BeSameAs(garbage);
    }
}
