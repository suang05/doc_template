using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;

public record SaveTemplateMappingsCommand(Guid TemplateId, List<SaveFieldMappingItemDto> Items);

public sealed class SaveTemplateMappingsUseCase(
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null) : IUseCase<SaveTemplateMappingsCommand>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task ExecuteAsync(SaveTemplateMappingsCommand command, CancellationToken ct = default)
    {
        var templateId = new TemplateId(command.TemplateId);
        var template = await _templateRepo.GetByIdWithDetailsAsync(templateId, ct)
            ?? await _templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' not found.");

        var now = _timeProvider.GetUtcNow();
        var mappings = command.Items.Select(item =>
        {
            var dsType = item.DataSourceType != null ? DataSourceType.FromString(item.DataSourceType) : DataSourceType.Json;
            var mapping = FieldMapping.Create(
                template.Id,
                item.Placeholder,
                item.SourcePath,
                item.Label ?? string.Empty,
                item.Required,
                item.SortOrder,
                now,
                dsType,
                item.DefaultValue,
                item.Transform);
            if (!string.IsNullOrWhiteSpace(item.DatasetAlias) || !string.IsNullOrWhiteSpace(item.ResultPath) || !string.IsNullOrWhiteSpace(item.MathExpression))
            {
                var aliasVo = !string.IsNullOrWhiteSpace(item.DatasetAlias) ? DatasetAlias.Create(item.DatasetAlias) : null;
                mapping.ConfigureDataSource(dsType, aliasVo, item.ResultPath, item.MathExpression, now);
            }
            return mapping;
        }).ToList();

        template.ReplaceFieldMappings(mappings, now);

        TemplateVersion? activeVersion = template.CurrentVersion;
        if (activeVersion is null && template.CurrentVersionId.HasValue)
        {
            activeVersion = template.FindVersion(template.CurrentVersionId.Value)
                ?? await _templateRepo.GetVersionByIdAsync(template.CurrentVersionId.Value, ct);
        }

        if (activeVersion != null && activeVersion.Status != TemplateVersionStatus.Archived)
        {
            activeVersion.UpdateMappingsSnapshot(FieldMappingSnapshotSerializer.Serialize(template.FieldMappings), now);
        }

        _templateRepo.Update(template);
        await _unitOfWork.CommitAsync(ct);
    }
}
