using System.Diagnostics;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Helpers;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;

public sealed class CommitTemplateDraftUseCase(
    ITemplateDraftCache draftCache,
    IStorageService storageService,
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork,
    ISchemaInferenceService schemaInferenceService,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IUseCase<CommitTemplateDraftCommand, CommitDraftResultDto>
{
    public async Task<CommitDraftResultDto> ExecuteAsync(CommitTemplateDraftCommand command, CancellationToken ct = default)
    {
        var projectId = projectContext.EnsureResolved("Committing a template draft requires an active project context.");

        var draft = await draftCache.GetAsync(projectId, command.DraftId, ct)
            ?? throw new DraftExpiredException(command.DraftId);

        if (await templateRepo.SlugExistsAsync(command.Slug, ct))
        {
            throw ConflictException.DuplicateSlug(command.Slug);
        }

        var now = timeProvider.GetUtcNow();
        var template = Template.Create(
            projectId,
            TemplateName.Create(command.Name),
            TemplateSlug.Create(command.Slug),
            command.Category,
            now);

        if (!TemplateFormat.TryFromExtension(draft.FileExtension, out var format))
        {
            throw new DomainValidationException($"Unsupported template file format: '{draft.FileExtension}'.");
        }

        var storageKey = StorageBuckets.TemplateVersionKey(template.ProjectId, template.Id, 1, format.Extension);
        var contentType = format == TemplateFormat.Html ? "text/html; charset=utf-8" : format.MimeType;

        string uploadedKey;
        await using (var draftStream = await draftCache.OpenStreamAsync(draft, ct))
        {
            var resultKey = await storageService.UploadAsync(StorageBuckets.Templates, storageKey, draftStream, contentType, ct);
            uploadedKey = string.IsNullOrWhiteSpace(resultKey) ? storageKey : resultKey;
        }

        try
        {
            var placeholderList = command.Mappings is { Count: > 0 }
                ? command.Mappings.Select(m => m.Placeholder).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : (draft.Placeholders ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            string? inferredSchema = schemaInferenceService?.InferSchemaFromPlaceholders(placeholderList, templateSlug: command.Slug);
            string? samplePayload = schemaInferenceService?.GenerateDefaultSamplePayload(placeholderList);

            var version = template.CreateDraftVersion(
                1,
                uploadedKey,
                format,
                projectContext.CallerIdentity ?? "system",
                now,
                "Initial upload via draft pipeline");
            version.UpdateDataSchema(inferredSchema, samplePayload, now);

            if (command.Mappings is { Count: > 0 })
            {
                var mappings = new List<FieldMapping>(command.Mappings.Count);
                foreach (var m in command.Mappings)
                {
                    var dsType = m.DataSourceType != null ? DataSourceType.FromString(m.DataSourceType) : DataSourceType.Json;
                    var fm = FieldMapping.Create(
                        template.Id,
                        m.Placeholder,
                        m.SourcePath,
                        m.Label ?? string.Empty,
                        m.Required,
                        m.SortOrder,
                        now,
                        dsType,
                        m.DefaultValue,
                        m.Transform);

                    if (!string.IsNullOrWhiteSpace(m.DatasetAlias) ||
                        !string.IsNullOrWhiteSpace(m.ResultPath) ||
                        !string.IsNullOrWhiteSpace(m.MathExpression))
                    {
                        var aliasVo = !string.IsNullOrWhiteSpace(m.DatasetAlias) ? DatasetAlias.Create(m.DatasetAlias) : null;
                        fm.ConfigureDataSource(dsType, aliasVo, m.ResultPath, m.MathExpression, now);
                    }

                    mappings.Add(fm);
                }

                template.ReplaceFieldMappings(mappings, now);
            }

            version.UpdateMappingsSnapshot(FieldMappingSnapshotSerializer.Serialize(template.FieldMappings), now);

            template.PublishVersion(version, now);

            await templateRepo.AddAsync(template, ct);
            await unitOfWork.CommitAsync(ct);
        }
        catch (Exception primaryEx)
        {
            try
            {
                await storageService.DeleteAsync(StorageBuckets.Templates, uploadedKey, CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                primaryEx.Data["OrphanedStorageKey"] = uploadedKey;
                primaryEx.Data["CompensatingDeleteError"] = cleanupEx.Message;
                Trace.TraceWarning(
                    "Failed to delete orphaned draft template object '{0}' after database commit failure: {1}",
                    uploadedKey,
                    cleanupEx.Message);
            }

            throw;
        }

        await draftCache.RemoveAsync(projectContext.ProjectId, command.DraftId, ct);
        return new CommitDraftResultDto(template.Id.Value);
    }
}
