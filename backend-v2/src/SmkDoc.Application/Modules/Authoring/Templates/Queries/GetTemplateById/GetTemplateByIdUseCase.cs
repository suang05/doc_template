using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.Templates;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.UseCases.Templates.Queries.GetTemplateById;

/// <summary>
/// Single-responsibility Query Use Case for retrieving a template by ID.
/// Injects only ITemplateRepository (Zero unused dependencies).
/// </summary>
public sealed class GetTemplateByIdUseCase(ITemplateRepository templateRepo) : IUseCase<GetTemplateByIdQuery, TemplateResponse>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;

    public async Task<TemplateResponse> ExecuteAsync(GetTemplateByIdQuery query, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(query.Id, ct)
            ?? throw new NotFoundException($"Template '{query.Id}' was not found.");

        return new TemplateResponse(
            template.Id,
            template.ProjectId,
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
