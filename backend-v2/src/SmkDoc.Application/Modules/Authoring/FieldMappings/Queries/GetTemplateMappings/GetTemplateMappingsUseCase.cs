using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateMappings;

public record GetTemplateMappingsQuery(Guid TemplateId);

public sealed class GetTemplateMappingsUseCase(
    ITemplateRepository templateRepo) : IUseCase<GetTemplateMappingsQuery, List<FieldMappingDto>>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;

    public async Task<List<FieldMappingDto>> ExecuteAsync(GetTemplateMappingsQuery query, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdWithDetailsAsync(query.TemplateId, ct);
        if (template == null) return [];

        return template.FieldMappings
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
