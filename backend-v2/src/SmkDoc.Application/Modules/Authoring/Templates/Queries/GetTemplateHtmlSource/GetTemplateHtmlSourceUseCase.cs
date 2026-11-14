using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateHtmlSource;

public sealed class GetTemplateHtmlSourceUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService) : IUseCase<GetTemplateHtmlSourceQuery, string>
{
    public async Task<string> ExecuteAsync(GetTemplateHtmlSourceQuery query, CancellationToken ct)
    {
        var template = await templateRepo.GetByIdAsync(new TemplateId(query.TemplateId), ct)
            ?? throw new NotFoundException($"Template '{query.TemplateId}' not found.");

        var version = template.GetPublishedVersion();

        if (version.FileFormat != null && version.FileFormat != TemplateFormat.Html)
            throw new BusinessRuleViolationException("Only HTML templates can be opened in the Monaco editor.");

        using var stream = await storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }
}
