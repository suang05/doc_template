using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateSchema;

public sealed class GetTemplateSchemaUseCase(
    ITemplateRepository templateRepo) : IUseCase<GetTemplateSchemaQuery, TemplateSchemaDto>
{
    public async Task<TemplateSchemaDto> ExecuteAsync(GetTemplateSchemaQuery query, CancellationToken ct = default)
    {
        var template = await templateRepo.GetByIdAsync(new TemplateId(query.TemplateId), ct)
            ?? throw new NotFoundException($"Template '{query.TemplateId}' not found.");

        TemplateVersion? activeVersion = null;
        if (template.CurrentVersionId.HasValue)
        {
            activeVersion = template.CurrentVersion
                ?? await templateRepo.GetVersionByIdAsync(template.CurrentVersionId.Value, ct);
        }

        return new TemplateSchemaDto(
            TemplateId: template.Id.Value,
            Slug: template.Slug,
            Format: activeVersion?.FileFormat,
            Version: activeVersion?.Version ?? 1,
            DataSchema: activeVersion?.DataSchema,
            SamplePayload: activeVersion?.SamplePayload
        );
    }
}
