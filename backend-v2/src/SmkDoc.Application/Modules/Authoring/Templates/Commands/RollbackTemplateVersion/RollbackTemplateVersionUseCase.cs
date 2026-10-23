using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;

public sealed record RollbackTemplateVersionCommand(Guid TemplateId, int TargetVersion);

/// <summary>
/// Single-responsibility Use Case for rolling back a template to an earlier version.
/// </summary>
public sealed class RollbackTemplateVersionUseCase(
    ITemplateRepository templateRepo,
    IStorageService storageService,
    IExecutionContext executionContext,
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null) : IUseCase<RollbackTemplateVersionCommand, int>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IStorageService _storageService = storageService;
    private readonly IExecutionContext _executionContext = executionContext;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<int> ExecuteAsync(RollbackTemplateVersionCommand command, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(new TemplateId(command.TemplateId), ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' was not found.");

        if (template.CurrentVersionId is null)
            throw new BusinessRuleViolationException($"Template '{template.Id}' has no published version.");

        var currentVersion = template.CurrentVersion
            ?? await _templateRepo.GetVersionByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new NotFoundException($"Current version record for template '{template.Id}' not found.");

        var versions = await _templateRepo.ListVersionsAsync(new TemplateId(command.TemplateId), ct);
        var archived = versions.FirstOrDefault(v => v.Version == command.TargetVersion)
            ?? throw new NotFoundException($"Version {command.TargetVersion} not found for template '{command.TemplateId}'.");

        using var archivedStream = await _storageService.DownloadAsync(StorageBuckets.Templates, archived.StorageKey, ct);
        using var memoryStream = new MemoryStream();
        await archivedStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        int nextVersionNumber = currentVersion.Version + 1;
        string ext = archived.FileFormat?.Extension ?? ".html";
        string contentType = archived.FileFormat?.MimeType ?? "text/html; charset=utf-8";
        string versionedKey = $"templates/{template.ProjectId}/archive/{template.Slug}_v{nextVersionNumber}{ext}";

        await _storageService.UploadAsync(StorageBuckets.Templates, versionedKey, memoryStream, contentType, ct);

        var now = _timeProvider.GetUtcNow();
        var newVersion = TemplateVersion.Draft(
            template.Id,
            nextVersionNumber,
            versionedKey,
            archived.FileFormat,
            _executionContext.CallerApp ?? "system",
            now,
            $"Rollback to v{command.TargetVersion}");

        newVersion.Publish(now);
        await _templateRepo.AddVersionAsync(newVersion, ct);

        template.SetCurrentVersion(newVersion.Id, now);
        _templateRepo.Update(template);

        await _unitOfWork.CommitAsync(ct);
        return nextVersionNumber;
    }
}
