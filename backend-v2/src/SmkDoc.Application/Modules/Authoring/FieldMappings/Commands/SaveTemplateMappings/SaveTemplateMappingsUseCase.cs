using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateMappings;

public sealed class SaveTemplateMappingsUseCase(
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<SaveTemplateMappingsCommand>
{
    public async Task ExecuteAsync(SaveTemplateMappingsCommand command, CancellationToken ct = default)
    {
        var templateId = new TemplateId(command.TemplateId);
        var template = await templateRepo.GetByIdWithDetailsAsync(templateId, ct)
            ?? await templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' not found.");

        var now = timeProvider.GetUtcNow();
        var mappings = command.Items.Select(item =>
        {
            var dsType = item.DataSourceType != null ? DataSourceType.FromString(item.DataSourceType) : DataSourceType.Json;
            var mapping = FieldMapping.Create(
                template.Id,
                item.Placeholder,
                item.SourcePath,
                item.Label ?? string.Empty,
                item.Required,
                item.SortOrder,
                now,
                dsType,
                item.DefaultValue,
                item.Transform);
            if (!string.IsNullOrWhiteSpace(item.DatasetAlias) || !string.IsNullOrWhiteSpace(item.ResultPath) || !string.IsNullOrWhiteSpace(item.MathExpression))
            {
                var aliasVo = !string.IsNullOrWhiteSpace(item.DatasetAlias) ? DatasetAlias.Create(item.DatasetAlias) : null;
                mapping.ConfigureDataSource(dsType, aliasVo, item.ResultPath, item.MathExpression, now);
            }
            return mapping;
        }).ToList();

        template.ReplaceFieldMappings(mappings, now);

        TemplateVersion? activeVersion = template.FindPublishedVersion();

        if (activeVersion != null && activeVersion.Status != TemplateVersionStatus.Archived)
        {
            activeVersion.UpdateMappingsSnapshot(FieldMappingSnapshotSerializer.Serialize(template.FieldMappings), now);
        }

        templateRepo.Update(template);
        await unitOfWork.CommitAsync(ct);
    }
}
