using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.Authoring.Fonts.Commands.UploadFont;

public sealed class UploadFontUseCaseTests
{
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly ProjectId _projectId = ProjectId.New();

    public UploadFontUseCaseTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(_projectId);
    }

    private UploadFontUseCase CreateSut() => new(_storageService, _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenValidTrueTypeFont_ShouldUploadToTenantPartitionedKeyAndReturnSafeName()
    {
        // Arrange: TrueType sfnt magic signature (00 01 00 00)
        using var stream = new MemoryStream([0x00, 0x01, 0x00, 0x00, 0x10, 0x20]);
        var command = new UploadFontCommand(stream, "Custom Font.ttf", "font/ttf");

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().Be("Custom_Font.ttf");
        await _storageService.Received(1).UploadAsync(
            StorageBuckets.Fonts,
            $"fonts/{_projectId.Value:N}/Custom_Font.ttf",
            Arg.Any<Stream>(),
            "font/ttf",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectContextUnresolved_ShouldThrowForbiddenException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        using var stream = new MemoryStream([0x00, 0x01, 0x00, 0x00]);
        var command = new UploadFontCommand(stream, "Font.ttf", "font/ttf");

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
        await _storageService.DidNotReceive().UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvalidContentType_ShouldThrowDomainValidationException()
    {
        // Arrange
        using var stream = new MemoryStream([0x00, 0x01, 0x00, 0x00]);
        var command = new UploadFontCommand(stream, "test.exe", "application/octet-stream");

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*Invalid font file type*");
        await _storageService.DidNotReceive().UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMagicBytesInvalid_ShouldThrowDomainValidationException()
    {
        // Arrange:MZ PE header disguised as .ttf
        using var stream = new MemoryStream([0x4D, 0x5A, 0x90, 0x00, 0x03]);
        var command = new UploadFontCommand(stream, "disguised.ttf", "font/ttf");

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*magic-byte signature*");
        await _storageService.DidNotReceive().UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("../evil.ttf")]
    [InlineData("..\\evil.ttf")]
    [InlineData("sub/evil.ttf")]
    public async Task ExecuteAsync_WhenFileNameContainsPathTraversal_ShouldThrowDomainValidationException(string maliciousFileName)
    {
        // Arrange
        using var stream = new MemoryStream([0x00, 0x01, 0x00, 0x00]);
        var command = new UploadFontCommand(stream, maliciousFileName, "font/ttf");

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*path traversal*");
        await _storageService.DidNotReceive().UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
