using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.Templates;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.UseCases.Templates;

public sealed class HtmlPersistenceUseCase(
    IRepository<Template> templateRepo,
    IRepository<TemplateVersion> versionRepo,
    IStorageService storageService,
    IUnitOfWork unitOfWork,
    IExecutionContext executionContext,
    ISchemaInferenceService schemaInferenceService) : IHtmlPersistenceUseCase
{
    private readonly IRepository<Template> _templateRepo = templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo = versionRepo;
    private readonly IStorageService _storageService = storageService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IExecutionContext _executionContext = executionContext;
    private readonly ISchemaInferenceService _schemaInferenceService = schemaInferenceService;

    public async Task<int> SaveHtmlVersionAsync(Guid templateId, SaveTemplateHtmlCommand request, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{templateId}' not found.");

        int currentVersionNumber = 0;
        if (template.CurrentVersionId.HasValue)
        {
            var currentVersion = await _versionRepo.GetByIdAsync(template.CurrentVersionId.Value, ct);
            if (currentVersion != null)
                currentVersionNumber = currentVersion.Version;
        }

        int nextVersionNumber = currentVersionNumber + 1;
        string versionedKey = $"templates/archive/{template.Slug}_v{nextVersionNumber}.html";
        string activeKey = $"templates/{template.Slug}.html";

        byte[] htmlBytes = Encoding.UTF8.GetBytes(request.Html ?? string.Empty);

        // 1. Upload archived historical version to MinIO
        using (var stream = new MemoryStream(htmlBytes))
            await _storageService.UploadAsync(StorageBuckets.Templates, versionedKey, stream, "text/html; charset=utf-8", ct);

        // 2. Upload latest active version to MinIO
        using (var stream = new MemoryStream(htmlBytes))
            await _storageService.UploadAsync(StorageBuckets.Templates, activeKey, stream, "text/html; charset=utf-8", ct);

        // 3. Infer JSON Schema and determine final sample payload
        var (inferredSchema, defaultSamplePayload) = _schemaInferenceService.InferFromHtml(
            request.Html ?? string.Empty,
            request.SamplePayload,
            template.Slug);
        string finalSamplePayload = !string.IsNullOrWhiteSpace(request.SamplePayload) && request.SamplePayload.Trim() != "{}"
            ? request.SamplePayload
            : defaultSamplePayload;

        // 4. Create new database version record (Legal Audit Trail with Schema & SamplePayload snapshots)
        var newVersion = new TemplateVersion(template.Id, nextVersionNumber, versionedKey, TemplateFormat.Html, _executionContext.CallerApp ?? "developer", request.ChangeNote);
        newVersion.UpdateDataSchema(inferredSchema, finalSamplePayload);
        newVersion.Publish();
        await _versionRepo.AddAsync(newVersion, ct);

        // 4. Update template active pointer and timestamp
        template.SetCurrentVersion(newVersion.Id);
        _templateRepo.Update(template);

        await _unitOfWork.CommitAsync(ct);
        return nextVersionNumber;
    }
}
