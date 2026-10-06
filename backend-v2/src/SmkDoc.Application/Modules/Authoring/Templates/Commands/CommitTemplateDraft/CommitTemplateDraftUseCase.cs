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
    IProjectRepository? projectRepo = null,
    ISchemaInferenceService? schemaInferenceService = null,
    IExecutionContext? executionContext = null,
    TimeProvider? timeProvider = null) : IUseCase<CommitTemplateDraftCommand, string>
{
    private readonly ITemplateDraftCache _draftCache = draftCache;
    private readonly IStorageService _storageService = storageService;
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo = versionRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IProjectRepository? _projectRepo = projectRepo;
    private readonly ISchemaInferenceService? _schemaInferenceService = schemaInferenceService;
    private readonly IExecutionContext? _executionContext = executionContext;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<string> ExecuteAsync(CommitTemplateDraftCommand command, CancellationToken ct = default)
    {
        var draft = await _draftCache.GetAsync(command.DraftId, ct)
            ?? throw new DraftExpiredException(command.DraftId);

        var format      = FormatFromExt(draft.FileExtension);
        var tempId      = Guid.CreateVersion7();
        var storageKey  = $"{tempId}/{Guid.CreateVersion7()}{draft.FileExtension}";
        var contentType = ContentTypeFromExt(draft.FileExtension);

        string uploadedKey;
        using (var ms = new MemoryStream(draft.FileBytes))
        {
            uploadedKey = await _storageService.UploadAsync(StorageBuckets.Templates, storageKey, ms, contentType, ct);
        }

        Guid targetProjectId = command.Request.ProjectId 
            ?? (_executionContext?.ProjectId.HasValue == true ? _executionContext.ProjectId.Value : Guid.Empty);

        if (targetProjectId == Guid.Empty)
        {
            throw new ValidationException("ProjectId", "ProjectId is required to commit template.");
        }

        var now = _timeProvider.GetUtcNow();
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
            string? inferredSchema = _schemaInferenceService?.InferSchemaFromPlaceholders(placeholderList, templateSlug: command.Request.Slug);
            string? samplePayload = _schemaInferenceService?.GenerateDefaultSamplePayload(placeholderList);

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

            await _templateRepo.AddAsync(template, ct);
            await _versionRepo.AddAsync(version, ct);
            await _unitOfWork.CommitAsync(ct);

            template.SetCurrentVersion(version.Id, now);
            _templateRepo.Update(template);
            await _unitOfWork.CommitAsync(ct);
        }
        catch
        {
            try { await _storageService.DeleteAsync(StorageBuckets.Templates, uploadedKey, CancellationToken.None); }
            catch { /* swallow rollback errors */ }
            throw;
        }

        await _draftCache.RemoveAsync(command.DraftId, ct);
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
