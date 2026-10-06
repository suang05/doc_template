using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

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
        var template = await _templateRepo.GetByIdWithDetailsAsync(command.TemplateId, ct)
            ?? await _templateRepo.GetByIdAsync(command.TemplateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' not found.");

        var now = _timeProvider.GetUtcNow();
        var mappings = command.Items.Select(item =>
        {
            var dsType = item.DataSourceType != null ? DataSourceType.FromString(item.DataSourceType) : DataSourceType.Json;
            var mapping = FieldMapping.Create(
                command.TemplateId,
                item.Placeholder,
                item.SourcePath,
                item.Label,
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
        await _unitOfWork.CommitAsync(ct);
    }
}
