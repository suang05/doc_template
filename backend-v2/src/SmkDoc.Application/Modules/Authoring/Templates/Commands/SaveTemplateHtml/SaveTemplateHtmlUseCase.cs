using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Storage;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Helpers;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.SaveTemplateHtml;

public sealed class SaveTemplateHtmlUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService,
    IUnitOfWork unitOfWork,
    ICurrentProjectContext projectContext,
    ISchemaInferenceService schemaInferenceService,
    TimeProvider timeProvider,
    IConcurrencyContext? concurrencyContext = null) : IUseCase<SaveTemplateHtmlCommand, SaveHtmlResultDto>
{
    public async Task<SaveHtmlResultDto> ExecuteAsync(SaveTemplateHtmlCommand command, CancellationToken ct)
    {
        var templateId = new TemplateId(command.TemplateId);
        var template = await templateRepo.GetByIdWithDetailsAsync(templateId, ct)
            ?? await templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' not found.");

        var currentVersion = template.CurrentVersion?.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ?? template.FindPublishedVersion()?.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)
            ?? "0";
        concurrencyContext?.EnsureMatches(template, currentVersion);

        int nextVersionNumber = template.GetNextVersionNumber();
        string versionedKey = StorageBuckets.TemplateVersionKey(template.ProjectId, template.Id, nextVersionNumber, ".html");
        // 1. Upload exclusively to immutable versioned key using Zero-LOH HybridSpoolStream
        await using (var stream = await HybridSpoolStream.CreateFromUtf8StringAsync(command.Html, ct: ct))
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

            // 3. Create append-only database version record and publish atomically
            var now = timeProvider.GetUtcNow();
            TemplateVersion? priorPublishedVersion = template.FindPublishedVersion();

            var newVersion = template.CreateNextVersion(
                versionedKey,
                TemplateFormat.Html,
                projectContext.CallerIdentity ?? "developer",
                now,
                command.ChangeNote);

            newVersion.UpdateDataSchema(inferredSchema, finalSamplePayload, now);

            var effectivePageSettings = command.PageSettings
                ?? priorPublishedVersion?.GetPageSettings()
                ?? TemplatePageSettings.Default;
            newVersion.UpdatePageSettings(effectivePageSettings, now);

            string mappingsSnapshot = "[]";
            if (template.FieldMappings.Count > 0)
            {
                mappingsSnapshot = FieldMappingSnapshotSerializer.Serialize(template.FieldMappings);
            }
            else if (!string.IsNullOrWhiteSpace(priorPublishedVersion?.MappingsSnapshot))
            {
                mappingsSnapshot = priorPublishedVersion.MappingsSnapshot;
            }

            newVersion.UpdateMappingsSnapshot(mappingsSnapshot, now);

            // 4. Persist aggregate update
            templateRepo.Update(template);
            await unitOfWork.CommitAsync(ct);

            return new SaveHtmlResultDto(nextVersionNumber);
        }
        catch (Exception primaryEx)
        {
            await StorageOrphanCleaner.TryDeleteOrphanAsync(
                storageService,
                StorageBuckets.Templates,
                versionedKey,
                primaryEx,
                CancellationToken.None);

            throw;
        }
    }
}
