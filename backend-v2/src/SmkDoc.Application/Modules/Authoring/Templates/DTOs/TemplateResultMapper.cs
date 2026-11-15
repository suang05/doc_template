using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.Authoring.Templates.DTOs;

/// <summary>
/// Maps <see cref="Template"/> aggregates to <see cref="TemplateResultDto"/>.
/// </summary>
public static class TemplateResultMapper
{
    public static TemplateResultDto ToResultDto(this Template template, string? fileFormatOverride = null)
    {
        ArgumentNullException.ThrowIfNull(template);

        return new TemplateResultDto(
            template.Id.Value,
            template.ProjectId.Value,
            template.Name.Value,
            template.Slug.Value,
            template.Category,
            template.IsActive,
            template.CurrentVersionId,
            fileFormatOverride ?? template.CurrentVersion?.FileFormat?.Name,
            template.CreatedAt,
            template.UpdatedAt,
            template.Version);
    }
}
