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
    IFieldMappingRepository mappingRepo,
    IUnitOfWork unitOfWork,
    IProjectRepository? projectRepo = null,
    ISchemaInferenceService? schemaInferenceService = null) : IUseCase<CommitTemplateDraftCommand, string>
{
    private readonly ITemplateDraftCache _draftCache = draftCache;
    private readonly IStorageService _storageService = storageService;
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo = versionRepo;
    private readonly IFieldMappingRepository _mappingRepo = mappingRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IProjectRepository? _projectRepo = projectRepo;
    private readonly ISchemaInferenceService? _schemaInferenceService = schemaInferenceService;

    public async Task<string> ExecuteAsync(CommitTemplateDraftCommand command, CancellationToken ct = default)
    {
        var draft = await _draftCache.GetAsync(command.DraftId, ct)
            ?? throw new DraftExpiredException(command.DraftId);

        var format      = FormatFromExt(draft.FileExtension);
        var templateId  = Guid.CreateVersion7();
        var versionId   = Guid.CreateVersion7();
        var storageKey  = $"{templateId}/{versionId}{draft.FileExtension}";
        var contentType = ContentTypeFromExt(draft.FileExtension);

        string uploadedKey;
        using (var ms = new MemoryStream(draft.FileBytes))
        {
            uploadedKey = await _storageService.UploadAsync(StorageBuckets.Templates, storageKey, ms, contentType, ct);
        }

        Guid targetProjectId = Guid.Empty;
        if (_projectRepo != null)
        {
            var defaultProj = await _projectRepo.GetDefaultAsync(ct);
            if (defaultProj != null) targetProjectId = defaultProj.Id;
        }

        try
        {
            var template = new Template(targetProjectId, command.Request.Name, command.Request.Slug, command.Request.Category)
            {
                Id = templateId
            };
            var placeholderList = command.Request.Mappings?.Select(m => m.Placeholder).ToList() ?? new List<string>();
            string? inferredSchema = _schemaInferenceService?.InferSchemaFromPlaceholders(placeholderList, templateSlug: command.Request.Slug);
            string? samplePayload = _schemaInferenceService?.GenerateDefaultSamplePayload(placeholderList);

            var version = new TemplateVersion(templateId, 1, uploadedKey, format, "system", "Initial upload via draft pipeline")
            {
                Id = versionId
            };
            version.UpdateDataSchema(inferredSchema, samplePayload);
            version.Publish();

            await _templateRepo.AddAsync(template, ct);
            await _versionRepo.AddAsync(version, ct);

            if (command.Request.Mappings != null)
            {
                foreach (var m in command.Request.Mappings)
                {
                    var dsType = m.DataSourceType != null ? DataSourceType.FromString(m.DataSourceType) : DataSourceType.Json;
                    var fm = new FieldMapping(templateId, m.Placeholder, m.SourcePath, m.Label, m.Required, m.SortOrder, dsType);
                    fm.UpdateMappingDetails(m.SourcePath, m.Label, m.Required, m.DefaultValue, m.Transform, m.SortOrder);
                    fm.ConfigureDataSource(dsType, m.DatasetAlias, m.ResultPath, m.MathExpression);
                    await _mappingRepo.AddAsync(fm, ct);
                }
            }

            await _unitOfWork.CommitAsync(ct);

            template.SetCurrentVersion(versionId);
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
        return templateId.ToString();
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
