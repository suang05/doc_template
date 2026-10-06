using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml;

public class SaveTemplateHtmlUseCase(
    ITemplateRepository templateRepo,
    IRepository<TemplateVersion> versionRepo,
    IStorageService storageService,
    IUnitOfWork unitOfWork,
    IExecutionContext executionContext,
    ISchemaInferenceService schemaInferenceService,
    TimeProvider? timeProvider = null) : IHtmlPersistenceUseCase
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo = versionRepo;
    private readonly IStorageService _storageService = storageService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IExecutionContext _executionContext = executionContext;
    private readonly ISchemaInferenceService _schemaInferenceService = schemaInferenceService;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<int> SaveHtmlVersionAsync(Guid templateId, SaveTemplateHtmlCommand request, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{templateId}' not found.");

        int currentVersionNumber = 0;
        if (template.CurrentVersionId.HasValue)
        {
            var currentVersion = await _versionRepo.GetByIdAsync(template.CurrentVersionId.Value, ct);
            currentVersionNumber = currentVersion?.Version ?? 0;
        }

        int nextVersionNumber = currentVersionNumber + 1;
        string versionedKey = $"templates/archive/{template.Slug}_v{nextVersionNumber}.html";
        string activeKey = $"templates/{template.Slug}.html";

        byte[] htmlBytes = Encoding.UTF8.GetBytes(request.Html ?? string.Empty);

        // 1. Upload archived and active versions to MinIO
        await UploadHtmlAsync(versionedKey, htmlBytes, ct);
        await UploadHtmlAsync(activeKey, htmlBytes, ct);

        // 2. Infer JSON Schema and determine final sample payload
        var (inferredSchema, defaultSamplePayload) = _schemaInferenceService.InferFromHtml(
            request.Html ?? string.Empty,
            request.SamplePayload,
            template.Slug);

        string finalSamplePayload = string.IsNullOrWhiteSpace(request.SamplePayload) || request.SamplePayload.Trim() == "{}"
            ? defaultSamplePayload
            : request.SamplePayload;

        // 3. Create new database version record
        var now = _timeProvider.GetUtcNow();
        var newVersion = TemplateVersion.Draft(
            template.Id,
            nextVersionNumber,
            versionedKey,
            TemplateFormat.Html,
            _executionContext.CallerApp ?? "developer",
            now,
            request.ChangeNote);
        newVersion.UpdateDataSchema(inferredSchema, finalSamplePayload, now);
        newVersion.Publish(now);
        await _versionRepo.AddAsync(newVersion, ct);

        // 4. Update template active pointer and timestamp
        template.SetCurrentVersion(newVersion.Id, now);
        _templateRepo.Update(template);

        await _unitOfWork.CommitAsync(ct);
        return nextVersionNumber;
    }

    private async Task UploadHtmlAsync(string key, byte[] bytes, CancellationToken ct)
    {
        using var stream = new MemoryStream(bytes);
        await _storageService.UploadAsync(StorageBuckets.Templates, key, stream, "text/html; charset=utf-8", ct);
    }
}
