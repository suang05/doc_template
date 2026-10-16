using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

/// <summary>
/// Single-responsibility Query Use Case for listing templates.
/// Uses ambient ICurrentProjectContext when available with query fallback.
/// </summary>
public sealed class ListTemplatesUseCase(
    ITemplateRepository templateRepo,
    ICurrentProjectContext? projectContext = null) : IUseCase<ListTemplatesQuery, List<TemplateResultDto>>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly ICurrentProjectContext? _projectContext = projectContext;

    public async Task<List<TemplateResultDto>> ExecuteAsync(ListTemplatesQuery query, CancellationToken ct = default)
    {
        var activeProjectId = query.ProjectId != Guid.Empty
            ? query.ProjectId
            : (_projectContext?.IsResolved == true ? _projectContext.ProjectId.Value : Guid.Empty);

        if (activeProjectId == Guid.Empty)
        {
            throw new DomainValidationException("ProjectId cannot be empty.");
        }

        var templates = await _templateRepo.ListAsync(ct);

        return templates
            .Select(t => new TemplateResultDto(
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
