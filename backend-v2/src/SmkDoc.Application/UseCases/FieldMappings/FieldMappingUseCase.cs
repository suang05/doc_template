using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.FieldMappings;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.UseCases.FieldMappings;

public sealed class FieldMappingUseCase(
    IRepository<FieldMapping> mappingRepo,
    IRepository<Template> templateRepo,
    IUnitOfWork unitOfWork)
{
    private readonly IRepository<FieldMapping> _mappingRepo = mappingRepo;
    private readonly IRepository<Template> _templateRepo = templateRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<List<FieldMappingDto>> GetMappingsByTemplateIdAsync(Guid templateId, CancellationToken ct = default)
    {
        var mappings = await _mappingRepo.ListAsync(m => m.TemplateId == templateId, ct);
        return mappings
            .OrderBy(m => m.SortOrder)
            .Select(m => new FieldMappingDto(
                m.Id,
                m.TemplateId,
                m.Placeholder,
                m.SourcePath,
                m.Label,
                m.Required,
                m.DefaultValue,
                m.Transform,
                m.SortOrder,
                m.DataSourceType.Value,
                m.DatasetAlias,
                m.ResultPath,
                m.MathExpression
            ))
            .ToList();
    }

    public async Task SaveMappingsAsync(Guid templateId, List<SaveFieldMappingItemDto> items, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{templateId}' not found.");

        var existing = await _mappingRepo.ListAsync(m => m.TemplateId == templateId, ct);
        foreach (var m in existing)
        {
            _mappingRepo.Remove(m);
        }

        foreach (var item in items)
        {
            var dsType = item.DataSourceType != null ? DataSourceType.FromString(item.DataSourceType) : DataSourceType.Json;
            var mapping = new FieldMapping(templateId, item.Placeholder, item.SourcePath, item.Label, item.Required, item.SortOrder, dsType);
            mapping.UpdateMappingDetails(item.SourcePath, item.Label, item.Required, item.DefaultValue, item.Transform, item.SortOrder);
            mapping.ConfigureDataSource(dsType, item.DatasetAlias, item.ResultPath, item.MathExpression);
            await _mappingRepo.AddAsync(mapping, ct);
        }

        await _unitOfWork.CommitAsync(ct);
    }
}
