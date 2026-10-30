using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;

public sealed record RollbackTemplateVersionCommand(Guid TemplateId, int TargetVersion);

/// <summary>
/// Single-responsibility Use Case for rolling back a template to an earlier version
/// using zero-copy immutable storage references and restoring snapshotted metadata.
/// </summary>
public sealed class RollbackTemplateVersionUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService,
    ICurrentProjectContext projectContext,
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null) : IUseCase<RollbackTemplateVersionCommand, int>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IStorageService _storageService = storageService;
    private readonly ICurrentProjectContext _projectContext = projectContext;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<int> ExecuteAsync(RollbackTemplateVersionCommand command, CancellationToken ct = default)
    {
        if (!_projectContext.IsResolved)
        {
            throw new ForbiddenException("Rolling back a template version requires an active project context.");
        }

        var templateId = new TemplateId(command.TemplateId);
        var template = await _templateRepo.GetByIdWithDetailsAsync(templateId, ct)
            ?? await _templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' was not found.");

        if (template.CurrentVersionId is null)
        {
            throw new BusinessRuleViolationException($"Template '{template.Id}' has no published version.");
        }

        var currentVersion = template.CurrentVersion
            ?? template.FindVersion(template.CurrentVersionId.Value)
            ?? await _templateRepo.GetVersionByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new NotFoundException($"Current version record for template '{template.Id}' not found.");

        IReadOnlyCollection<TemplateVersion> allVersions = template.Versions.Count > 0
            ? template.Versions
            : (await _templateRepo.ListVersionsAsync(templateId, ct) ?? []);

        var targetVersion = allVersions.FirstOrDefault(v => v.Version == command.TargetVersion)
            ?? throw new NotFoundException($"Version {command.TargetVersion} not found for template '{command.TemplateId}'.");

        if (string.IsNullOrWhiteSpace(targetVersion.StorageKey) ||
            !await _storageService.ExistsAsync(StorageBuckets.Templates, targetVersion.StorageKey, ct))
        {
            throw new NotFoundException(
                $"Storage asset '{targetVersion.StorageKey}' for template version {command.TargetVersion} was not found.");
        }

        int maxVersion = allVersions.Count > 0
            ? Math.Max(allVersions.Max(v => v.Version), currentVersion.Version)
            : currentVersion.Version;
        int nextVersionNumber = maxVersion + 1;

        var now = _timeProvider.GetUtcNow();
        if (currentVersion.Status == TemplateVersionStatus.Published)
        {
            currentVersion.Archive(now);
        }

        var newVersion = template.CreateDraftVersion(
            nextVersionNumber,
            targetVersion.StorageKey,
            targetVersion.FileFormat,
            _projectContext.CallerIdentity ?? "system",
            now,
            $"Rollback to v{command.TargetVersion}");

        newVersion.UpdateDataSchema(targetVersion.DataSchema, targetVersion.SamplePayload, now);
        newVersion.UpdateMappingsSnapshot(targetVersion.MappingsSnapshot, now);

        if (targetVersion.MappingsSnapshot is not null)
        {
            var restoredMappings = FieldMappingSnapshotSerializer.Deserialize(template.Id, targetVersion.MappingsSnapshot, now);
            template.ReplaceFieldMappings(restoredMappings, now);
        }

        template.PublishVersion(newVersion, now);
        _templateRepo.Update(template);

        await _unitOfWork.CommitAsync(ct);
        return nextVersionNumber;
    }
}
