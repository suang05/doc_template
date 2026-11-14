using System.Text.RegularExpressions;
using SmkDoc.Application.Common;
using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;

public sealed partial class UploadFontUseCase(
    IStorageService storageService,
    ICurrentProjectContext projectContext) : IUseCase<UploadFontCommand, UploadFontResultDto>
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "font/woff2",
        "font/woff",
        "font/ttf",
        "font/otf",
        "application/x-font-ttf",
        "application/x-font-opentype",
        "application/font-woff",
        "application/font-woff2"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf", ".otf", ".woff", ".woff2"
    };

    [GeneratedRegex(@"^[a-zA-Z0-9._-]+$", RegexOptions.None, 250)]
    private static partial Regex SafeFileNameRegex();

    public async Task<UploadFontResultDto> ExecuteAsync(UploadFontCommand command, CancellationToken ct)
    {
        // Step 1: Parse & Guard (Fail-fast at line 1 via Smart Context Property)
        var projectId = projectContext.GetRequiredProjectId();

        if (string.IsNullOrWhiteSpace(command.ContentType) || !AllowedContentTypes.Contains(command.ContentType.Trim()))
        {
            throw new DomainValidationException("Invalid font file type. Allowed: WOFF2, WOFF, TTF, OTF.");
        }

        string safeFileName = SanitizeAndValidateFileName(command.FileName);

        using var buffer = new MemoryStream();
        await command.Data.CopyToAsync(buffer, ct);
        if (buffer.Length < 4)
        {
            throw new DomainValidationException("Font file is empty or truncated.");
        }

        buffer.Position = 0;
        Span<byte> header = stackalloc byte[4];
        _ = buffer.Read(header);

        if (!HasValidFontMagicHeader(header))
        {
            throw new DomainValidationException("Uploaded file failed font magic-byte signature verification.");
        }

        buffer.Position = 0;
        var objectKey = StorageBuckets.TenantFontKey(projectId, safeFileName);
        await storageService.UploadAsync(StorageBuckets.Fonts, objectKey, buffer, command.ContentType.Trim(), ct);
        return new UploadFontResultDto("Font uploaded successfully.", safeFileName);
    }

    public static string SanitizeAndValidateFileName(string? rawFileName)
    {
        if (string.IsNullOrWhiteSpace(rawFileName))
        {
            throw new DomainValidationException("Font file name cannot be empty.");
        }

        if (rawFileName.Contains("..", StringComparison.Ordinal) || rawFileName.Contains('/', StringComparison.Ordinal) || rawFileName.Contains('\\', StringComparison.Ordinal))
        {
            throw new DomainValidationException("Font file name must not contain path traversal segments or directory separators.");
        }

        string normalized = Path.GetFileName(rawFileName.Trim()).Replace(' ', '_');
        if (string.IsNullOrWhiteSpace(normalized) || !SafeFileNameRegex().IsMatch(normalized))
        {
            throw new DomainValidationException("Font file name contains invalid characters. Use ASCII letters, digits, '.', '_', or '-'.");
        }

        string ext = Path.GetExtension(normalized);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new DomainValidationException("Invalid font file extension. Allowed: .ttf, .otf, .woff, .woff2.");
        }

        return normalized;
    }

    private static bool HasValidFontMagicHeader(ReadOnlySpan<byte> header)
    {
        // TrueType (00 01 00 00) or Apple TrueType ('true' = 74 72 75 65)
        if ((header[0] == 0x00 && header[1] == 0x01 && header[2] == 0x00 && header[3] == 0x00) ||
            (header[0] == 0x74 && header[1] == 0x72 && header[2] == 0x75 && header[3] == 0x65))
        {
            return true;
        }

        // OpenType CFF ('OTTO' = 4F 54 54 4F)
        if (header[0] == 0x4F && header[1] == 0x54 && header[2] == 0x54 && header[3] == 0x4F)
        {
            return true;
        }

        // WOFF ('wOFF' = 77 4F 46 46)
        if (header[0] == 0x77 && header[1] == 0x4F && header[2] == 0x46 && header[3] == 0x46)
        {
            return true;
        }

        // WOFF2 ('wOF2' = 77 4F 46 32)
        if (header[0] == 0x77 && header[1] == 0x4F && header[2] == 0x46 && header[3] == 0x32)
        {
            return true;
        }

        return false;
    }
}
