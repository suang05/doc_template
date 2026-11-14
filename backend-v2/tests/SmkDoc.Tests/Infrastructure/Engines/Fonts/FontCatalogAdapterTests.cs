using SmkDoc.Application.Common;
using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Engines.Fonts;

namespace SmkDoc.Tests.Infrastructure.Engines.Fonts;

public sealed class FontCatalogAdapterTests
{
    private readonly IStorageService _storage = Substitute.For<IStorageService>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly ProjectId _projectId = ProjectId.New();

    public FontCatalogAdapterTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.GetRequiredProjectId().Returns(_projectId);
    }

    private FontCatalogAdapter CreateSut() => new(_storage, _projectContext);

    [Fact]
    public void GetCuratedGoogleFonts_ShouldContainStandardThaiFonts()
    {
        var sut = CreateSut();

        var googleFonts = sut.GetCuratedGoogleFonts();

        googleFonts.Should().NotBeEmpty();
        googleFonts.Should().Contain(f => f.FontFamily == "Sarabun" && f.Source == FontSource.GoogleFonts);
        googleFonts.Should().Contain(f => f.FontFamily == "Prompt" && f.Source == FontSource.GoogleFonts);
        googleFonts.Should().Contain(f => f.FontFamily == "Kanit" && f.Source == FontSource.GoogleFonts);
        googleFonts.Should().Contain(f => f.FontFamily == "Mitr" && f.Source == FontSource.GoogleFonts);
    }

    [Fact]
    public async Task GetCatalogAsync_WhenProjectResolved_ShouldCombineGoogleFontsAndMinIOFonts()
    {
        // Arrange
        var prefix = $"fonts/{_projectId.Value:N}/";
        _storage.ListObjectsAsync(StorageBuckets.Fonts, prefix, Arg.Any<CancellationToken>())
            .Returns([$"fonts/{_projectId.Value:N}/Corporate-Logo.ttf", $"fonts/{_projectId.Value:N}/Invoice-Sign.otf"]);

        var sut = CreateSut();

        // Act
        var catalog = await sut.GetCatalogAsync(CancellationToken.None);

        // Assert
        catalog.GoogleFonts.Should().Contain(f => f.FontFamily == "Sarabun");
        catalog.CustomFonts.Should().HaveCount(2);
        catalog.CustomFonts.Should().Contain(f => f.FontFamily == "Corporate-Logo" && f.Source == FontSource.MinIO);
        catalog.CustomFonts.Should().Contain(f => f.FontFamily == "Invoice-Sign" && f.Source == FontSource.MinIO);
        catalog.AllFontNames.Should().Contain("Corporate-Logo.ttf");
        catalog.AllFontNames.Should().Contain("Invoice-Sign.otf");
        catalog.AllFontNames.Should().Contain("Sarabun");
        catalog.AllFontNames.Should().Contain("Sarabun-Regular.ttf");
    }

    [Fact]
    public async Task GetCatalogAsync_WhenProjectNotResolved_ShouldThrowTenantContextNotResolvedException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        _projectContext.GetRequiredProjectId().Returns(_ => throw new TenantContextNotResolvedException());
        var sut = CreateSut();

        // Act
        var act = () => sut.GetCatalogAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TenantContextNotResolvedException>();
    }

    [Fact]
    public async Task GetFontBase64Async_WhenTenantFontExists_DownloadsFromTenantKey()
    {
        // Arrange
        var tenantKey = $"fonts/{_projectId.Value:N}/Kanit-Bold.ttf";
        byte[] fontBytes = [0x00, 0x01, 0x00, 0x00, 0xAA, 0xBB];
        _storage.ExistsAsync(StorageBuckets.Fonts, tenantKey, Arg.Any<CancellationToken>()).Returns(true);
        _storage.DownloadAsync(StorageBuckets.Fonts, tenantKey, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(fontBytes));

        var sut = CreateSut();

        // Act
        var base64 = await sut.GetFontBase64Async("Kanit-Bold.ttf", CancellationToken.None);

        // Assert
        base64.Should().Be(Convert.ToBase64String(fontBytes));
    }

    [Fact]
    public async Task GetFontBase64Async_WhenLargePayloadWithPartialChunks_EncodesExactBase64UsingPooledBuffers()
    {
        // Arrange
        var tenantKey = $"fonts/{_projectId.Value:N}/LargeThai.ttf";
        var fontBytes = new byte[65_537]; // > 24KB chunk size, not divisible by 3
        System.Security.Cryptography.RandomNumberGenerator.Fill(fontBytes);

        using var trickleStream = new TrickleReadStream(fontBytes, maxChunkSize: 1_001);
        _storage.ExistsAsync(StorageBuckets.Fonts, tenantKey, Arg.Any<CancellationToken>()).Returns(true);
        _storage.DownloadAsync(StorageBuckets.Fonts, tenantKey, Arg.Any<CancellationToken>())
            .Returns(trickleStream);

        var sut = CreateSut();

        // Act
        var base64 = await sut.GetFontBase64Async("LargeThai.ttf", CancellationToken.None);

        // Assert
        base64.Should().Be(Convert.ToBase64String(fontBytes));
    }

    [Fact]
    public async Task GetFontBase64Async_WhenPathTraversalSupplied_ThrowsDomainValidationException()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var act = () => sut.GetFontBase64Async("../other-tenant/Secret.ttf", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*path traversal*");
    }

    [Fact]
    public async Task GetFontBase64Async_WhenProjectNotResolved_ThrowsTenantContextNotResolvedException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        _projectContext.GetRequiredProjectId().Returns(_ => throw new TenantContextNotResolvedException());
        var sut = CreateSut();

        // Act
        var act = () => sut.GetFontBase64Async("Sarabun-Regular.ttf", CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TenantContextNotResolvedException>();
    }

    private sealed class TrickleReadStream(byte[] payload, int maxChunkSize) : Stream
    {
        private int _offset;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => payload.Length;
        public override long Position { get => _offset; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (_offset >= payload.Length)
            {
                return 0;
            }

            int toCopy = Math.Min(Math.Min(buffer.Length, maxChunkSize), payload.Length - _offset);
            payload.AsSpan(_offset, toCopy).CopyTo(buffer);
            _offset += toCopy;
            return toCopy;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Read(buffer.Span));

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
