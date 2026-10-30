using System.Diagnostics;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Helpers;
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
    IProjectRepository projectRepo,
    ISchemaInferenceService schemaInferenceService,
    ICurrentProjectContext projectContext,
    TimeProvider? timeProvider = null) : IUseCase<CommitTemplateDraftCommand, string>
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<string> ExecuteAsync(CommitTemplateDraftCommand command, CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Committing a template draft requires an active project context.");
        }

        var draft = await draftCache.GetAsync(command.DraftId, ct)
            ?? throw new DraftExpiredException(command.DraftId);

        if (projectRepo != null)
        {
            _ = await projectRepo.GetByIdAsync(projectContext.ProjectId, ct)
                ?? throw new NotFoundException($"Project '{projectContext.ProjectId}' not found.");
        }

        if (await templateRepo.SlugExistsAsync(command.Request.Slug, ct))
        {
            throw ConflictException.DuplicateSlug(command.Request.Slug);
        }

        var now = _timeProvider.GetUtcNow();
        var template = Template.Create(
            projectContext.ProjectId,
            TemplateName.Create(command.Request.Name),
            TemplateSlug.Create(command.Request.Slug),
            command.Request.Category,
            now);

        var format = FormatFromExt(draft.FileExtension);
        var storageKey = StorageBuckets.TemplateVersionKey(template.ProjectId, template.Id, 1, draft.FileExtension);
        var contentType = ContentTypeFromExt(draft.FileExtension);

        string uploadedKey;
        using (var ms = new MemoryStream(draft.FileBytes))
        {
            var resultKey = await storageService.UploadAsync(StorageBuckets.Templates, storageKey, ms, contentType, ct);
            uploadedKey = string.IsNullOrWhiteSpace(resultKey) ? storageKey : resultKey;
        }

        try
        {
            var placeholderList = command.Request.Mappings?.Select(m => m.Placeholder).ToList() ?? [];
            string? inferredSchema = schemaInferenceService?.InferSchemaFromPlaceholders(placeholderList, templateSlug: command.Request.Slug);
            string? samplePayload = schemaInferenceService?.GenerateDefaultSamplePayload(placeholderList);

            var version = template.CreateDraftVersion(
                1,
                uploadedKey,
                format,
                projectContext.CallerIdentity ?? "system",
                now,
                "Initial upload via draft pipeline");
            version.UpdateDataSchema(inferredSchema, samplePayload, now);

            if (command.Request.Mappings is { Count: > 0 })
            {
                var mappings = new List<FieldMapping>(command.Request.Mappings.Count);
                foreach (var m in command.Request.Mappings)
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
                version.UpdateMappingsSnapshot(FieldMappingSnapshotSerializer.Serialize(template.FieldMappings), now);
            }

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

        await draftCache.RemoveAsync(command.DraftId, ct);
        return template.Id.Value.ToString();
    }

    private static TemplateFormat FormatFromExt(string ext) => ext switch
    {
        ".xlsx" => TemplateFormat.Xlsx,
        ".docx" => TemplateFormat.Docx,
        _ => TemplateFormat.Html,
    };

    private static string ContentTypeFromExt(string ext) => ext switch
    {
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "text/html; charset=utf-8",
    };
}
