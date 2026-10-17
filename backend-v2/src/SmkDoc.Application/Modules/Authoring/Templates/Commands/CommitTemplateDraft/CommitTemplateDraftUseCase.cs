using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
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
    IRepository<TemplateVersion> versionRepo,
    IUnitOfWork unitOfWork,
    IProjectRepository projectRepo,
    ISchemaInferenceService schemaInferenceService,
    IExecutionContext executionContext,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IUseCase<CommitTemplateDraftCommand, string>
{
    private readonly TimeProvider timeProvider = timeProvider;

    public async Task<string> ExecuteAsync(CommitTemplateDraftCommand command, CancellationToken ct = default)
    {
        var draft = await draftCache.GetAsync(command.DraftId, ct)
            ?? throw new DraftExpiredException(command.DraftId);

        Guid targetProjectId = (command.Request.ProjectId.HasValue && command.Request.ProjectId.Value != Guid.Empty)
            ? command.Request.ProjectId.Value
            : (projectContext?.IsResolved == true ? projectContext.ProjectId.Value : (executionContext?.ProjectId.HasValue == true ? executionContext.ProjectId.Value : Guid.Empty));

        if (targetProjectId == Guid.Empty)
        {
            throw new ValidationException("ProjectId", "ProjectId is required to commit template.");
        }

        if (projectRepo != null)
        {
            var project = await projectRepo.GetByIdAsync(targetProjectId, ct)
                ?? throw new NotFoundException($"Project '{targetProjectId}' not found.");
        }

        var format      = FormatFromExt(draft.FileExtension);
        var tempId      = Guid.CreateVersion7();
        var storageKey  = $"templates/{targetProjectId}/{tempId}/{Guid.CreateVersion7()}{draft.FileExtension}";
        var contentType = ContentTypeFromExt(draft.FileExtension);

        string uploadedKey;
        using (var ms = new MemoryStream(draft.FileBytes))
        {
            uploadedKey = await storageService.UploadAsync(StorageBuckets.Templates, storageKey, ms, contentType, ct);
        }

        var now = this.timeProvider.GetUtcNow();
        Guid committedTemplateId;

        try
        {
            var template = Template.Create(
                targetProjectId,
                TemplateName.Create(command.Request.Name),
                TemplateSlug.Create(command.Request.Slug),
                command.Request.Category,
                now);
            committedTemplateId = template.Id;

            var placeholderList = command.Request.Mappings?.Select(m => m.Placeholder).ToList() ?? new List<string>();
            string? inferredSchema = schemaInferenceService?.InferSchemaFromPlaceholders(placeholderList, templateSlug: command.Request.Slug);
            string? samplePayload = schemaInferenceService?.GenerateDefaultSamplePayload(placeholderList);

            var version = TemplateVersion.Draft(
                template.Id,
                1,
                uploadedKey,
                format,
                "system",
                now,
                "Initial upload via draft pipeline");
            version.UpdateDataSchema(inferredSchema, samplePayload, now);
            version.Publish(now);

            template.AddVersion(version, now);

            if (command.Request.Mappings != null)
            {
                var mappings = new List<FieldMapping>();
                foreach (var m in command.Request.Mappings)
                {
                    var dsType = m.DataSourceType != null ? DataSourceType.FromString(m.DataSourceType) : DataSourceType.Json;
                    var fm = FieldMapping.Create(
                        template.Id,
                        m.Placeholder,
                        m.SourcePath,
                        m.Label,
                        m.Required,
                        m.SortOrder,
                        now,
                        dsType,
                        m.DefaultValue,
                        m.Transform);
                    if (!string.IsNullOrWhiteSpace(m.DatasetAlias) || !string.IsNullOrWhiteSpace(m.ResultPath) || !string.IsNullOrWhiteSpace(m.MathExpression))
                    {
                        var aliasVo = !string.IsNullOrWhiteSpace(m.DatasetAlias) ? DatasetAlias.Create(m.DatasetAlias) : null;
                        fm.ConfigureDataSource(dsType, aliasVo, m.ResultPath, m.MathExpression, now);
                    }
                    mappings.Add(fm);
                }
                template.ReplaceFieldMappings(mappings, now);
            }

            template.SetCurrentVersion(version.Id, now);

            await templateRepo.AddAsync(template, ct);
            await versionRepo.AddAsync(version, ct);
            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            try { await storageService.DeleteAsync(StorageBuckets.Templates, uploadedKey, CancellationToken.None); }
            catch { /* swallow rollback errors */ }
            throw;
        }

        await draftCache.RemoveAsync(command.DraftId, ct);
        return committedTemplateId.ToString();
    }

    private static TemplateFormat FormatFromExt(string ext) => ext switch
    {
        ".xlsx" => TemplateFormat.Xlsx,
        ".docx" => TemplateFormat.Docx,
        _       => TemplateFormat.Html,
    };

    private static string ContentTypeFromExt(string ext) => ext switch
    {
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _       => "text/html; charset=utf-8",
    };
}
