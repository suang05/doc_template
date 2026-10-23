using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;

/// <summary>
/// Single-responsibility Query Use Case for retrieving a template by ID.
/// Injects only ITemplateRepository (Zero unused dependencies).
/// </summary>
public sealed class GetTemplateByIdUseCase(ITemplateRepository templateRepo) : IUseCase<GetTemplateByIdQuery, TemplateResultDto>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;

    public async Task<TemplateResultDto> ExecuteAsync(GetTemplateByIdQuery query, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(new TemplateId(query.Id), ct)
            ?? throw new NotFoundException($"Template '{query.Id}' was not found.");

        return new TemplateResultDto(
            template.Id.Value,
            template.ProjectId.Value,
            template.Name,
            template.Slug,
            template.Category,
            template.IsActive,
            template.CurrentVersionId,
            template.CurrentVersion?.FileFormat?.Name,
            template.CreatedAt,
            template.UpdatedAt
        );
    }
}
