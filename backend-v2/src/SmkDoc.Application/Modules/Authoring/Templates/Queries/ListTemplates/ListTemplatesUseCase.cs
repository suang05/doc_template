using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

/// <summary>
/// Single-responsibility Query Use Case for listing templates.
/// Injects only ITemplateRepository (Zero unused dependencies).
/// </summary>
public sealed class ListTemplatesUseCase(ITemplateRepository templateRepo) : IUseCase<ListTemplatesQuery, List<TemplateResponse>>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;

    public async Task<List<TemplateResponse>> ExecuteAsync(ListTemplatesQuery query, CancellationToken ct = default)
    {
        if (query.ProjectId == Guid.Empty)
        {
            throw new DomainValidationException("ProjectId cannot be empty.");
        }

        var templates = await _templateRepo.ListByProjectAsync(query.ProjectId, ct);

        return templates
            .Select(t => new TemplateResponse(
                t.Id,
                t.ProjectId,
                t.Name,
                t.Slug,
                t.Category,
                t.IsActive,
                t.CurrentVersionId,
                t.CurrentVersion?.FileFormat?.Name,
                t.CreatedAt,
                t.UpdatedAt
            ))
            .ToList();
    }
}
