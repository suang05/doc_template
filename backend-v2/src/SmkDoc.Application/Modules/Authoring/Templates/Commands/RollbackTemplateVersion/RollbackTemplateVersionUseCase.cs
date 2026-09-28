using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;

public sealed record RollbackTemplateVersionCommand(Guid TemplateId, int TargetVersion);

/// <summary>
/// Single-responsibility Use Case for rolling back a template to an earlier version.
/// </summary>
public sealed class RollbackTemplateVersionUseCase(
    ITemplateRepository templateRepo,
    IRepository<TemplateVersion> versionRepo,
    IStorageService storageService,
    IExecutionContext executionContext,
    IUnitOfWork unitOfWork) : IUseCase<RollbackTemplateVersionCommand, int>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo = versionRepo;
    private readonly IStorageService _storageService = storageService;
    private readonly IExecutionContext _executionContext = executionContext;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<int> ExecuteAsync(RollbackTemplateVersionCommand command, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(command.TemplateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' was not found.");

        if (template.CurrentVersionId is null)
            throw new InvalidOperationException($"Template '{template.Id}' has no published version.");

        var currentVersion = await _versionRepo.GetByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new InvalidOperationException($"Current version record for template '{template.Id}' not found.");

        var archived = await _versionRepo.FirstOrDefaultAsync(
            v => v.TemplateId == command.TemplateId && v.Version == command.TargetVersion, ct)
            ?? throw new NotFoundException($"Version {command.TargetVersion} not found for template '{command.TemplateId}'.");

        using var archivedStream = await _storageService.DownloadAsync(StorageBuckets.Templates, archived.StorageKey, ct);
        using var memoryStream = new MemoryStream();
        await archivedStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        int nextVersionNumber = currentVersion.Version + 1;
        string ext = archived.FileFormat?.Extension ?? ".html";
        string contentType = archived.FileFormat?.MimeType ?? "text/html; charset=utf-8";
        string versionedKey = $"templates/archive/{template.Slug}_v{nextVersionNumber}{ext}";

        await _storageService.UploadAsync(StorageBuckets.Templates, versionedKey, memoryStream, contentType, ct);

        var newVersion = new TemplateVersion(
            template.Id,
            nextVersionNumber,
            versionedKey,
            archived.FileFormat,
            _executionContext.CallerApp ?? "system",
            $"Rollback to v{command.TargetVersion}");

        newVersion.Publish();
        await _versionRepo.AddAsync(newVersion, ct);

        template.SetCurrentVersion(newVersion.Id);
        _templateRepo.Update(template);

        await _unitOfWork.CommitAsync(ct);
        return nextVersionNumber;
    }
}
