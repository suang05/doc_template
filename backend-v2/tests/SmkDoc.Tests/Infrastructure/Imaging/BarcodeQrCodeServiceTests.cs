using FluentAssertions;
using SmkDoc.Infrastructure.Imaging;
using Xunit;

namespace SmkDoc.Tests;

public class BarcodeQrCodeServiceTests
{
    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Fact]
    public void BarcodeService_Generate_ShouldReturnValidPngImageBytes()
    {
        var sut = new BarcodeService();
        var bytes = sut.Generate("SMK-DOC-2026-9999", 300, 100);

        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(8);
        bytes.Take(8).Should().Equal(PngHeader);
    }

    [Fact]
    public void QrCodeService_Generate_ShouldReturnValidPngImageBytes()
    {
        var sut = new QrCodeService();
        var bytes = sut.Generate("https://sammakorn.co.th/docs/verify?ref=DOC-12345", 5);

        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(8);
        bytes.Take(8).Should().Equal(PngHeader);
    }
}
