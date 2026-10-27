using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateStudioBundle;

public sealed class GetTemplateStudioBundleUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService) : IUseCase<GetTemplateStudioBundleQuery, TemplateStudioDto>
{
    public async Task<TemplateStudioDto> ExecuteAsync(GetTemplateStudioBundleQuery query, CancellationToken ct = default)
    {
        var template = await templateRepo.GetByIdAsync(new TemplateId(query.TemplateId), ct)
            ?? throw new NotFoundException($"Template '{query.TemplateId}' not found.");

        if (template.CurrentVersionId == null)
            throw new BusinessRuleViolationException($"Template '{query.TemplateId}' has no active published version.");

        var version = template.CurrentVersion
            ?? await templateRepo.GetVersionByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new NotFoundException($"Active version for template '{query.TemplateId}' not found.");

        if (version.FileFormat != null && version.FileFormat != TemplateFormat.Html)
            throw new BusinessRuleViolationException("Only HTML templates can be opened in the Monaco editor.");

        using var stream = await storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string html = await reader.ReadToEndAsync(ct);

        return new TemplateStudioDto(
            Html: html,
            SamplePayload: version.SamplePayload,
            DataSchema: version.DataSchema,
            Version: version.Version
        );
    }
}
