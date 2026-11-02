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
    TimeProvider timeProvider) : IUseCase<SaveTemplateHtmlCommand, int>
{
    public async Task<int> ExecuteAsync(SaveTemplateHtmlCommand command, CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Saving template HTML requires an active project context.");
        }

        var templateId = new TemplateId(command.TemplateId);
        var template = await templateRepo.GetByIdWithDetailsAsync(templateId, ct)
            ?? await templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' not found.");

        TemplateVersion? currentVersion = template.CurrentVersion;
        if (currentVersion is null && template.CurrentVersionId.HasValue)
        {
            currentVersion = template.FindVersion(template.CurrentVersionId.Value)
                ?? await templateRepo.GetVersionByIdAsync(template.CurrentVersionId.Value, ct);
        }

        int maxVersionNumber = template.Versions.Count > 0
            ? template.Versions.Max(v => v.Version)
            : 0;

        if (maxVersionNumber == 0)
        {
            var history = await templateRepo.ListVersionsAsync(templateId, ct);
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
        byte[] htmlBytes = Encoding.UTF8.GetBytes(command.Html ?? string.Empty);

        // 1. Upload exclusively to immutable versioned key (zero mutable activeKey overwrite)
        using (var stream = new MemoryStream(htmlBytes))
        {
            await storageService.UploadAsync(StorageBuckets.Templates, versionedKey, stream, "text/html; charset=utf-8", ct);
        }

        try
        {
            // 2. Infer JSON Schema and determine final sample payload
            var (inferredSchema, defaultSamplePayload) = schemaInferenceService.InferFromHtml(
                command.Html ?? string.Empty,
                command.SamplePayload,
                template.Slug);

            string finalSamplePayload = string.IsNullOrWhiteSpace(command.SamplePayload) || command.SamplePayload.Trim() == "{}"
                ? defaultSamplePayload
                : command.SamplePayload;

            // 3. Create append-only database version record and archive previous Published version
            var now = timeProvider.GetUtcNow();
            if (currentVersion != null && currentVersion.Status == TemplateVersionStatus.Published)
            {
                currentVersion.Archive(now);
            }

            var newVersion = template.CreateDraftVersion(
                nextVersionNumber,
                versionedKey,
                TemplateFormat.Html,
                projectContext.CallerIdentity ?? "developer",
                now,
                command.ChangeNote);
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
            templateRepo.Update(template);
            await unitOfWork.CommitAsync(ct);

            return nextVersionNumber;
        }
        catch (Exception primaryEx)
        {
            try
            {
                await storageService.DeleteAsync(StorageBuckets.Templates, versionedKey, CancellationToken.None);
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
