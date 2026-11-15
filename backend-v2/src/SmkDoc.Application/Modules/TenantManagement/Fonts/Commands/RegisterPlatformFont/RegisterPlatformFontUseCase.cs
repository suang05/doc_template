using System.Security.Cryptography;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Storage;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Application.Modules.TenantManagement.Fonts.DTOs;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.TenantManagement.Fonts.Commands.RegisterPlatformFont;

public sealed class RegisterPlatformFontUseCase(
    IStorageService storageService,
    IFontRepository fontRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<RegisterPlatformFontCommand, PlatformFontResultDto>
{
    private const long MaxFontFileBytes = 20L * 1024 * 1024;

    public async Task<PlatformFontResultDto> ExecuteAsync(RegisterPlatformFontCommand command, CancellationToken ct)
    {
        var safeFileName = Path.GetFileName(command.FileName).Trim();
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            throw new ValidationException("FileName", "Font file name is invalid.");
        }

        await using var spooled = await HybridSpoolStream.SpoolFromAsync(command.Data, ct: ct);
        if (spooled.Length == 0)
        {
            throw new ValidationException("Data", "Font file cannot be empty.");
        }

        if (spooled.Length > MaxFontFileBytes)
        {
            throw new ValidationException("Data", $"Font file exceeds the maximum limit of {MaxFontFileBytes / (1024 * 1024)} MB.");
        }

        var format = await DetectAndValidateFormatAsync(spooled, ct);
        spooled.Position = 0;
        var hashBytes = await SHA256.HashDataAsync(spooled, ct);
        var checksumHex = Convert.ToHexStringLower(hashBytes);

        spooled.Position = 0;
        var storageKey = StorageBuckets.PlatformFontKey(safeFileName);
        await storageService.UploadAsync(StorageBuckets.Fonts, storageKey, spooled, command.ContentType, ct);

        bool committed = false;
        try
        {
            var now = timeProvider.GetUtcNow();
            var familyName = string.IsNullOrWhiteSpace(command.FamilyName)
                ? Path.GetFileNameWithoutExtension(safeFileName)
                : command.FamilyName.Trim();

            var font = Font.Register(
                familyName,
                command.Weight,
                command.Style,
                format,
                storageKey,
                new Sha256Hash(checksumHex),
                isSystemFont: true,
                projectId: null,
                now);

            await fontRepository.AddAsync(font, ct);
            await unitOfWork.CommitAsync(ct);
            committed = true;

            return new PlatformFontResultDto(
                font.Id.Value,
                font.FamilyName,
                font.Weight,
                font.Style,
                font.Format,
                font.StorageKey,
                font.ChecksumSha256.Value,
                font.IsSystemFont,
                font.ProjectId?.Value,
                font.CreatedAt,
                font.Version);
        }
        finally
        {
            if (!committed)
            {
                await StorageOrphanCleaner.TryDeleteOrphanAsync(
                    storageService,
                    StorageBuckets.Fonts,
                    storageKey,
                    new InvalidOperationException("Platform font registration rolled back before commit."),
                    CancellationToken.None);
            }
        }
    }

    private static async Task<string> DetectAndValidateFormatAsync(Stream spooled, CancellationToken ct)
    {
        if (spooled.Length < 4)
        {
            throw new ValidationException("Data", "File is too short to be a valid TTF, OTF, WOFF, or WOFF2 font.");
        }

        byte[] header = new byte[4];
        await spooled.ReadExactlyAsync(header, ct);

        if ((header[0] == 0x00 && header[1] == 0x01 && header[2] == 0x00 && header[3] == 0x00) ||
            (header[0] == 't' && header[1] == 'r' && header[2] == 'u' && header[3] == 'e') ||
            (header[0] == 't' && header[1] == 'y' && header[2] == 'p' && header[3] == '1'))
        {
            return "ttf";
        }

        if (header[0] == 'O' && header[1] == 'T' && header[2] == 'T' && header[3] == 'O')
        {
            return "otf";
        }

        if (header[0] == 'w' && header[1] == 'O' && header[2] == 'F' && header[3] == 'F')
        {
            return "woff";
        }

        if (header[0] == 'w' && header[1] == 'O' && header[2] == 'F' && header[3] == '2')
        {
            return "woff2";
        }

        throw new ValidationException("Data", "Unsupported font binary header. Expected TTF, OTF, WOFF, or WOFF2.");
    }
}
