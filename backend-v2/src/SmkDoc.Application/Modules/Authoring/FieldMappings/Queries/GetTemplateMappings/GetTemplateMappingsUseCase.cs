using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateMappings;

public record GetTemplateMappingsQuery(Guid TemplateId);

public sealed class GetTemplateMappingsUseCase(
    IFieldMappingRepository mappingRepo) : IUseCase<GetTemplateMappingsQuery, List<FieldMappingDto>>
{
    private readonly IFieldMappingRepository _mappingRepo = mappingRepo;

    public async Task<List<FieldMappingDto>> ExecuteAsync(GetTemplateMappingsQuery query, CancellationToken ct = default)
    {
        var mappings = await _mappingRepo.GetByTemplateIdAsync(query.TemplateId, ct);
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
}
