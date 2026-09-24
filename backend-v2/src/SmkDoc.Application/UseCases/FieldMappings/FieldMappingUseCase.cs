using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.UseCases.FieldMappings;

public class FieldMappingUseCase
{
    private readonly IRepository<FieldMapping> _mappingRepo;
    private readonly IRepository<Template> _templateRepo;
    private readonly IUnitOfWork _unitOfWork;

    public FieldMappingUseCase(
        IRepository<FieldMapping> mappingRepo,
        IRepository<Template> templateRepo,
        IUnitOfWork unitOfWork)
    {
        _mappingRepo = mappingRepo;
        _templateRepo = templateRepo;
        _unitOfWork = unitOfWork;
    }

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
                m.DataSourceType,
                m.DatasetAlias,
                m.ResultPath,
                m.MathExpression
            ))
            .ToList();
    }

    public async Task SaveMappingsAsync(Guid templateId, List<SaveFieldMappingItem> items, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new KeyNotFoundException($"Template '{templateId}' not found.");

        var existing = await _mappingRepo.ListAsync(m => m.TemplateId == templateId, ct);
        foreach (var m in existing)
        {
            _mappingRepo.Remove(m);
        }

        foreach (var item in items)
        {
            var mapping = new FieldMapping
            {
                Id = Guid.NewGuid(),
                TemplateId = templateId,
                Placeholder = item.Placeholder,
                SourcePath = item.SourcePath,
                Label = item.Label,
                Required = item.Required,
                DefaultValue = item.DefaultValue,
                Transform = item.Transform,
                SortOrder = item.SortOrder,
                DataSourceType = item.DataSourceType,
                DatasetAlias   = item.DatasetAlias,
                ResultPath     = item.ResultPath,
                MathExpression = item.MathExpression
            };
            await _mappingRepo.AddAsync(mapping, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }
}
