using System.Diagnostics;
using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml;

public sealed class SaveTemplateHtmlUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService,
    IUnitOfWork unitOfWork,
    ICurrentProjectContext projectContext,
    ISchemaInferenceService schemaInferenceService,
    TimeProvider? timeProvider = null) : IUseCase<SaveTemplateHtmlCommand, int>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IStorageService _storageService = storageService;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICurrentProjectContext _projectContext = projectContext;
    private readonly ISchemaInferenceService _schemaInferenceService = schemaInferenceService;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<int> ExecuteAsync(SaveTemplateHtmlCommand request, CancellationToken ct = default)
    {
        if (!_projectContext.IsResolved)
        {
            throw new ForbiddenException("Saving template HTML requires an active project context.");
        }

        var templateId = new TemplateId(request.TemplateId);
        var template = await _templateRepo.GetByIdWithDetailsAsync(templateId, ct)
            ?? await _templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{request.TemplateId}' not found.");

        TemplateVersion? currentVersion = template.CurrentVersion;
        if (currentVersion is null && template.CurrentVersionId.HasValue)
        {
            currentVersion = template.FindVersion(template.CurrentVersionId.Value)
                ?? await _templateRepo.GetVersionByIdAsync(template.CurrentVersionId.Value, ct);
        }

        int maxVersionNumber = template.Versions.Count > 0
            ? template.Versions.Max(v => v.Version)
            : 0;

        if (maxVersionNumber == 0)
        {
            var history = await _templateRepo.ListVersionsAsync(templateId, ct);
            if (history is { Count: > 0 })
            {
                maxVersionNumber = history.Max(v => v.Version);
            }
        }

        if (currentVersion != null && currentVersion.Version > maxVersionNumber)
        {
            maxVersionNumber = currentVersion.Version;
        }

        int nextVersionNumber = maxVersionNumber + 1;
        string versionedKey = StorageBuckets.TemplateVersionKey(template.ProjectId, template.Id, nextVersionNumber, ".html");
        byte[] htmlBytes = Encoding.UTF8.GetBytes(request.Html ?? string.Empty);

        // 1. Upload exclusively to immutable versioned key (zero mutable activeKey overwrite)
        using (var stream = new MemoryStream(htmlBytes))
        {
            await _storageService.UploadAsync(StorageBuckets.Templates, versionedKey, stream, "text/html; charset=utf-8", ct);
        }

        try
        {
            // 2. Infer JSON Schema and determine final sample payload
            var (inferredSchema, defaultSamplePayload) = _schemaInferenceService.InferFromHtml(
                request.Html ?? string.Empty,
                request.SamplePayload,
                template.Slug);

            string finalSamplePayload = string.IsNullOrWhiteSpace(request.SamplePayload) || request.SamplePayload.Trim() == "{}"
                ? defaultSamplePayload
                : request.SamplePayload;

            // 3. Create append-only database version record and archive previous Published version
            var now = _timeProvider.GetUtcNow();
            if (currentVersion != null && currentVersion.Status == TemplateVersionStatus.Published)
            {
                currentVersion.Archive(now);
            }

            var newVersion = template.CreateDraftVersion(
                nextVersionNumber,
                versionedKey,
                TemplateFormat.Html,
                _projectContext.CallerIdentity ?? "developer",
                now,
                request.ChangeNote);
            newVersion.UpdateDataSchema(inferredSchema, finalSamplePayload, now);

            string? mappingsSnapshot = template.FieldMappings.Count > 0
                ? FieldMappingSnapshotSerializer.Serialize(template.FieldMappings)
                : currentVersion?.MappingsSnapshot;
            if (!string.IsNullOrWhiteSpace(mappingsSnapshot))
            {
                newVersion.UpdateMappingsSnapshot(mappingsSnapshot, now);
            }

            template.PublishVersion(newVersion, now);

            // 4. Persist aggregate update
            _templateRepo.Update(template);
            await _unitOfWork.CommitAsync(ct);

            return nextVersionNumber;
        }
        catch (Exception primaryEx)
        {
            try
            {
                await _storageService.DeleteAsync(StorageBuckets.Templates, versionedKey, CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                primaryEx.Data["OrphanedStorageKey"] = versionedKey;
                primaryEx.Data["CompensatingDeleteError"] = cleanupEx.Message;
                Trace.TraceWarning(
                    "Failed to delete orphaned HTML template object '{0}' after database commit failure: {1}",
                    versionedKey,
                    cleanupEx.Message);
            }

            throw;
        }
    }
}
