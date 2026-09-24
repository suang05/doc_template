using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SmkDoc.Infrastructure.Security;
using Xunit;

namespace SmkDoc.Tests;

public class DocxSecurityScannerServiceTests
{
    private readonly DocxSecurityScannerService _sut = new(NullLogger<DocxSecurityScannerService>.Instance);

    [Fact]
    public void Scan_WithNonDocxStream_ReturnsFalseWithScanFailedThreat()
    {
        using var stream = new MemoryStream("not a docx file"u8.ToArray());
        var result = _sut.Scan(stream);
        result.IsSafe.Should().BeFalse();
        result.Threats.Should().ContainSingle(t => t.StartsWith("Scan failed:") || t.Contains("main document part"));
    }

    [Fact]
    public void Scan_WithNonDocxStream_RestoresStreamPosition()
    {
        using var stream = new MemoryStream("garbage"u8.ToArray());
        stream.Position = 0;
        _sut.Scan(stream);
        stream.Position.Should().Be(0);
    }

    [Fact]
    public void Scan_WithEmptyStream_ReturnsFalse()
    {
        using var stream = new MemoryStream();
        var result = _sut.Scan(stream);
        result.IsSafe.Should().BeFalse();
        result.Threats.Should().NotBeEmpty();
    }
}
