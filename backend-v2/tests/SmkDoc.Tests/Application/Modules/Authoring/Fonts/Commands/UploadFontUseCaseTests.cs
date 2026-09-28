using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Fonts.Commands;

public class UploadFontUseCaseTests
{
    private readonly Mock<IStorageService> _storageService = new();

    private UploadFontUseCase CreateSut() => new(_storageService.Object);

    [Fact]
    public async Task ExecuteAsync_WhenValidFont_ShouldUploadAndReturnObjectName()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UploadFontCommand(stream, "Custom Font.ttf", "font/ttf");

        var result = await CreateSut().ExecuteAsync(command);

        result.Should().Be("Custom_Font.ttf");
        _storageService.Verify(s => s.UploadAsync("fonts", "Custom_Font.ttf", stream, "font/ttf", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvalidContentType_ShouldThrowArgumentException()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UploadFontCommand(stream, "test.exe", "application/octet-stream");

        Func<Task> act = () => CreateSut().ExecuteAsync(command);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid font file type*");
    }
}
