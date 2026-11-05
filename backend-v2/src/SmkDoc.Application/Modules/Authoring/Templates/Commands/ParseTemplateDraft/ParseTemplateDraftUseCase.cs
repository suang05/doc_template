using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Services;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.ParseTemplateDraft;

public sealed class ParseTemplateDraftUseCase(
    ITemplateDraftHandlerRegistry draftHandlerRegistry,
    ITemplateDraftCache draftCache,
    ICurrentProjectContext projectContext) : IUseCase<ParseTemplateDraftCommand, ParseDraftResultDto>
{
    private const long MaxDraftFileSizeBytes = 20L * 1024 * 1024;

    public async Task<ParseDraftResultDto> ExecuteAsync(ParseTemplateDraftCommand command, CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Uploading a template draft requires an active project context.");
        }

        var ext = Path.GetExtension(command.FileName).ToLowerInvariant();
        var handler = draftHandlerRegistry.GetHandler(ext);

        await using var spooled = await HybridSpoolStream.SpoolFromAsync(command.FileStream, ct: ct);
        if (spooled.Length > MaxDraftFileSizeBytes)
        {
            throw new DomainValidationException(
                $"Draft file size ({spooled.Length / (1024 * 1024)} MB) exceeds the 20 MB limit.");
        }

        spooled.Position = 0;
        await handler.ValidateSecurityAsync(spooled, ct);

        spooled.Position = 0;
        var detailedPlaceholders = await handler.ExtractDetailedPlaceholdersAsync(spooled, ct);
        var placeholders = detailedPlaceholders.Select(p => p.Name).Distinct().ToList();

        spooled.Position = 0;
        var draftId = await draftCache.StoreAsync(
            projectContext.ProjectId,
            spooled,
            command.FileName,
            ext,
            placeholders,
            ct);

        return new ParseDraftResultDto(draftId, placeholders, detailedPlaceholders);
    }
}
