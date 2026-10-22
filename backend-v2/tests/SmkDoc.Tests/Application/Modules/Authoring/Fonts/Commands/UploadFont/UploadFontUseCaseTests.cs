using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;

namespace SmkDoc.Tests.Application.Modules.Authoring.Fonts.Commands.UploadFont;

public sealed class UploadFontUseCaseTests
{
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();

    private UploadFontUseCase CreateSut() => new(_storageService);

    [Fact]
    public async Task ExecuteAsync_WhenValidFont_ShouldUploadAndReturnObjectName()
    {
        // Arrange
        using var stream = new MemoryStream([1, 2, 3]);
        var command = new UploadFontCommand(stream, "Custom Font.ttf", "font/ttf");

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().Be("Custom_Font.ttf");
        await _storageService.Received(1).UploadAsync("fonts", "Custom_Font.ttf", stream, "font/ttf", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvalidContentType_ShouldThrowArgumentException()
    {
        // Arrange
        using var stream = new MemoryStream([1, 2, 3]);
        var command = new UploadFontCommand(stream, "test.exe", "application/octet-stream");

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid font file type*");
        await _storageService.DidNotReceive().UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
