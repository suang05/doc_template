using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.DownloadTemplate;

public sealed record DownloadTemplateQuery(Guid Id);

public sealed record DownloadTemplateResult(
    Stream Stream,
    string ContentType,
    string FileName
);

/// <summary>
/// Single-responsibility Query Use Case for downloading a template file.
/// </summary>
public sealed class DownloadTemplateUseCase(
    ITemplateRepository templateRepo,
    IRepository<TemplateVersion> versionRepo,
    IStorageService storageService) : IUseCase<DownloadTemplateQuery, DownloadTemplateResult>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo = versionRepo;
    private readonly IStorageService _storageService = storageService;

    public async Task<DownloadTemplateResult> ExecuteAsync(DownloadTemplateQuery query, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(query.Id, ct)
            ?? throw new NotFoundException($"Template '{query.Id}' was not found.");

        if (template.CurrentVersionId is null)
            throw new InvalidOperationException($"Template '{template.Id}' has no published version.");

        var version = await _versionRepo.GetByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new InvalidOperationException($"Current version record for template '{template.Id}' not found.");

        var stream = await _storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        string ext = Path.GetExtension(version.StorageKey).ToLowerInvariant();
        string contentType = ext switch
        {
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "text/html; charset=utf-8"
        };

        return new DownloadTemplateResult(stream, contentType, $"{template.Slug}{ext}");
    }
}
