using SmkDoc.Application.Common.Extensions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateMappings;

public sealed class GetTemplateMappingsUseCase(
    ITemplateRepository templateRepo) : IUseCase<GetTemplateMappingsQuery, List<FieldMappingDto>>
{
    public async Task<List<FieldMappingDto>> ExecuteAsync(GetTemplateMappingsQuery query, CancellationToken ct = default)
    {
        var template = await templateRepo.GetByIdWithDetailsAsync(new TemplateId(query.TemplateId), ct);
        if (template == null) return [];

        return template.FieldMappings
            .OrderBy(m => m.SortOrder)
            .Select(m => new FieldMappingDto(
                m.Id,
                m.TemplateId.Value,
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
