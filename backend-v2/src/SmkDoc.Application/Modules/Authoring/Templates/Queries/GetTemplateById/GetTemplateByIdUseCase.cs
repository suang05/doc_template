using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;

/// <summary>
/// Single-responsibility Query Use Case for retrieving a template by ID.
/// Injects only ITemplateRepository (Zero unused dependencies).
/// </summary>
public sealed class GetTemplateByIdUseCase(ITemplateRepository templateRepo) : IUseCase<GetTemplateByIdQuery, TemplateResultDto>
{
    public async Task<TemplateResultDto> ExecuteAsync(GetTemplateByIdQuery query, CancellationToken ct)
    {
        var template = await templateRepo.GetByIdAsync(new TemplateId(query.Id), ct)
            ?? throw new NotFoundException($"Template '{query.Id}' was not found.");

        return template.ToResultDto();
    }
}
