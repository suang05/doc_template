using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SmkDoc.Infrastructure.Imaging;
using Xunit;

namespace SmkDoc.Tests;

public class ImageOptimizerServiceTests
{
    private readonly ImageOptimizerService _sut = new(NullLogger<ImageOptimizerService>.Instance);

    [Fact]
    public void Optimize_WithNullBytes_ReturnsEmpty()
    {
        var result = _sut.Optimize(null!);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Optimize_WithSmallImage_ReturnsSameReference()
    {
        // Under MinSizeForOptimization (50 KB) → no processing
        var tiny = new byte[1024];
        var result = _sut.Optimize(tiny);
        result.Should().BeSameAs(tiny);
    }

    [Fact]
    public void Optimize_WithInvalidImageData_ReturnsSameBytes()
    {
        // 60 KB of random bytes — not a valid image
        var garbage = new byte[60 * 1024];
        new Random(42).NextBytes(garbage);
        var result = _sut.Optimize(garbage);
        result.Should().BeSameAs(garbage);
    }
}
