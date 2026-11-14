using SmkDoc.Application.Common;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Helpers;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;

/// <summary>
/// Single-responsibility Use Case for rolling back a template to an earlier version
/// using zero-copy immutable storage references and restoring snapshotted metadata.
/// </summary>
public sealed class RollbackTemplateVersionUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService,
    ICurrentProjectContext projectContext,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<RollbackTemplateVersionCommand, RollbackVersionResultDto>
{
    public async Task<RollbackVersionResultDto> ExecuteAsync(RollbackTemplateVersionCommand command, CancellationToken ct)
    {
        var templateId = new TemplateId(command.TemplateId);
        var template = await templateRepo.GetByIdWithDetailsAsync(templateId, ct)
            ?? await templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' was not found.");

        _ = template.GetPublishedVersion();

        var targetVersion = template.FindVersion(command.TargetVersion)
            ?? throw new NotFoundException($"Version {command.TargetVersion} not found for template '{command.TemplateId}'.");

        if (string.IsNullOrWhiteSpace(targetVersion.StorageKey) ||
            !await storageService.ExistsAsync(StorageBuckets.Templates, targetVersion.StorageKey, ct))
        {
            throw new NotFoundException(
                $"Storage asset '{targetVersion.StorageKey}' for template version {command.TargetVersion} was not found.");
        }

        var now = timeProvider.GetUtcNow();
        var newVersion = template.RollbackToVersion(command.TargetVersion, projectContext.CallerIdentity ?? "system", now);

        if (targetVersion.MappingsSnapshot is not null)
        {
            var restoredMappings = FieldMappingSnapshotSerializer.Deserialize(template.Id, targetVersion.MappingsSnapshot, now);
            template.ReplaceFieldMappings(restoredMappings, now);
        }

        templateRepo.Update(template);
        await unitOfWork.CommitAsync(ct);

        return new RollbackVersionResultDto(newVersion.Version);
    }
}
