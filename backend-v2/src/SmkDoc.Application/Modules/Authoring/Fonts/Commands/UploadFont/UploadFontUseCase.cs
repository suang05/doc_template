using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;

public record UploadFontCommand(Stream Data, string FileName, string ContentType);

public sealed class UploadFontUseCase(IStorageService storageService) : IUseCase<UploadFontCommand, string>
{
    private readonly IStorageService _storageService = storageService;
    private const string FontsBucket = "fonts";

    public async Task<string> ExecuteAsync(UploadFontCommand command, CancellationToken ct = default)
    {
        var allowedTypes = new[] { "font/woff2", "font/woff", "font/ttf", "application/x-font-ttf" };
        if (!allowedTypes.Contains(command.ContentType.ToLowerInvariant()))
        {
            throw new ArgumentException("Invalid font file type. Allowed: WOFF2, WOFF, TTF.");
        }

        var objectName = command.FileName.Replace(" ", "_");
        await _storageService.UploadAsync(FontsBucket, objectName, command.Data, command.ContentType, ct);
        return objectName;
    }
}
