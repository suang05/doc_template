using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.Templates;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.UseCases.Templates;

public sealed class TemplateManagementUseCase(
    IRepository<Template> templateRepo,
    IRepository<TemplateVersion> versionRepo,
    IStorageService storageService,
    ITemplateScannerService scanner,
    IDocxSecurityScanner securityScanner,
    IExecutionContext executionContext,
    IUnitOfWork unitOfWork,
    IRepository<Project>? projectRepo = null)
{
    private readonly IRepository<Template> _templateRepo = templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo = versionRepo;
    private readonly IStorageService _storageService = storageService;
    private readonly ITemplateScannerService _scanner = scanner;
    private readonly IDocxSecurityScanner _securityScanner = securityScanner;
    private readonly IExecutionContext _executionContext = executionContext;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IRepository<Project>? _projectRepo = projectRepo;

    public async Task<List<TemplateDto>> ListTemplatesAsync(CancellationToken ct = default)
    {
        var templates = await _templateRepo.ListAsync(null, ct);
        var currentVersionIds = templates.Where(t => t.CurrentVersionId.HasValue).Select(t => t.CurrentVersionId!.Value).ToList();
        var versions = await _versionRepo.ListAsync(v => currentVersionIds.Contains(v.Id), ct);
        var versionDict = versions.ToDictionary(v => v.Id, v => v.FileFormat);

        return templates
            .OrderByDescending(t => t.UpdatedAt)
            .Select(t => new TemplateDto(
                t.Id, t.Name, t.Slug, t.Category, t.IsActive,
                t.CurrentVersionId, 
                t.CurrentVersionId.HasValue && versionDict.TryGetValue(t.CurrentVersionId.Value, out var fmt) ? fmt : null,
                t.CreatedAt, t.UpdatedAt ?? t.CreatedAt))
            .ToList();
    }

    public async Task<TemplateDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Template with ID '{id}' not found.");

        TemplateFormat? format = null;
        if (t.CurrentVersionId.HasValue)
        {
            var version = await _versionRepo.GetByIdAsync(t.CurrentVersionId.Value, ct);
            format = version?.FileFormat;
        }

        return new TemplateDto(t.Id, t.Name, t.Slug, t.Category, t.IsActive,
            t.CurrentVersionId, format, t.CreatedAt, t.UpdatedAt ?? t.CreatedAt);
    }

    public async Task<TemplateDto> CreateTemplateAsync(
        CreateTemplateCommand request,
        Stream? fileStream = null,
        string? fileName = null,
        CancellationToken ct = default)
    {
        var existing = await _templateRepo.FirstOrDefaultAsync(t => t.Slug == request.Slug, ct);
        if (existing != null)
            throw ConflictException.DuplicateSlug(request.Slug);

        string storageKey;
        TemplateFormat fileFormat;

        if (fileStream != null && !string.IsNullOrWhiteSpace(fileName))
        {
            string ext = Path.GetExtension(fileName).ToLowerInvariant();

            if (ext == ".docx")
            {
                // WordprocessingDocument.Open takes ownership and disposes its argument stream.
                // Scan a copy so the upload stream remains open and at position 0.
                using var scanCopy = new MemoryStream();
                await fileStream.CopyToAsync(scanCopy, ct);
                scanCopy.Position = 0;
                fileStream.Position = 0;

                var scanResult = _securityScanner.Scan(scanCopy);
                if (!scanResult.IsSafe)
                    throw new InvalidOperationException(
                        $"DOCX file failed security scan: {string.Join("; ", scanResult.Threats)}");
            }

            storageKey = $"templates/{request.Slug}{ext}";
            fileFormat = ext switch
            {
                ".docx" => TemplateFormat.Docx,
                ".xlsx" => TemplateFormat.Xlsx,
                _       => TemplateFormat.Html
            };
            string contentType = fileFormat.MimeType;
            await _storageService.UploadAsync(StorageBuckets.Templates, storageKey, fileStream, contentType, ct);
        }
        else
        {
            storageKey = $"templates/{request.Slug}.html";
            fileFormat = TemplateFormat.Html;
            string htmlContent = request.HtmlContent
                ?? "<!DOCTYPE html>\n<html lang=\"th\">\n<head>\n  <meta charset=\"UTF-8\" />\n</head>\n<body>\n  <h2>Template</h2>\n</body>\n</html>";
            using var htmlStream = new MemoryStream(Encoding.UTF8.GetBytes(htmlContent));
            await _storageService.UploadAsync(StorageBuckets.Templates, storageKey, htmlStream, "text/html; charset=utf-8", ct);
        }

        Guid targetProjectId = Guid.Empty;
        if (_projectRepo != null)
        {
            var defaultProj = await _projectRepo.FirstOrDefaultAsync(p => p.IsActive, ct)
                ?? await _projectRepo.FirstOrDefaultAsync(p => true, ct);
            if (defaultProj != null) targetProjectId = defaultProj.Id;
        }

        var template = new Template(targetProjectId, request.Name, request.Slug, request.Category);
        await _templateRepo.AddAsync(template, ct);
        await _unitOfWork.CommitAsync(ct); // Save template first to generate ID and break circular dependency

        var initialVersion = new TemplateVersion(template.Id, 1, storageKey, fileFormat, _executionContext.CallerApp ?? "system", "Initial version");
        initialVersion.Publish();
        await _versionRepo.AddAsync(initialVersion, ct);
        await _unitOfWork.CommitAsync(ct); // Save version first to generate ID

        template.SetCurrentVersion(initialVersion.Id);
        _templateRepo.Update(template);
        await _unitOfWork.CommitAsync(ct); // Update template with CurrentVersionId

        return new TemplateDto(template.Id, template.Name, template.Slug, template.Category,
            template.IsActive, template.CurrentVersionId, initialVersion.FileFormat, template.CreatedAt, template.UpdatedAt ?? template.CreatedAt);
    }

    public async Task<string> GetTemplateHtmlAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Template '{id}' not found.");

        var version = await GetCurrentVersionAsync(template, ct);
        
        if (version.FileFormat != null && version.FileFormat != TemplateFormat.Html)
            throw new InvalidOperationException("Only HTML templates can be opened in the HTML source editor.");

        using var stream = await _storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }

    public async Task<int> SaveTemplateHtmlAsync(Guid id, SaveTemplateHtmlCommand request, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Template '{id}' not found.");

        var currentVersion = await GetCurrentVersionAsync(template, ct);
        int nextVersionNumber = currentVersion.Version + 1;

        string versionedKey = $"templates/archive/{template.Slug}_v{nextVersionNumber}.html";
        string activeKey = $"templates/{template.Slug}.html";

        byte[] htmlBytes = Encoding.UTF8.GetBytes(request.Html);

        using (var stream = new MemoryStream(htmlBytes))
            await _storageService.UploadAsync(StorageBuckets.Templates, versionedKey, stream, "text/html; charset=utf-8", ct);

        using (var stream = new MemoryStream(htmlBytes))
            await _storageService.UploadAsync(StorageBuckets.Templates, activeKey, stream, "text/html; charset=utf-8", ct);

        var newVersion = new TemplateVersion(template.Id, nextVersionNumber, versionedKey, TemplateFormat.Html, _executionContext.CallerApp ?? "developer", request.ChangeNote);
        newVersion.Publish();
        await _versionRepo.AddAsync(newVersion, ct);

        template.SetCurrentVersion(newVersion.Id);
        _templateRepo.Update(template);

        await _unitOfWork.CommitAsync(ct);
        return nextVersionNumber;
    }

    public async Task UpdateMetadataAsync(Guid id, UpdateTemplateMetadataCommand request, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Template '{id}' not found.");

        template.UpdateDetails(request.Name ?? template.Name, request.Category ?? template.Category);
        if (request.IsActive.HasValue) 
        { 
            if (request.IsActive.Value) template.Activate(); 
            else template.Deactivate(); 
        }

        _templateRepo.Update(template);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task DeactivateTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Template '{id}' not found.");

        template.Deactivate();
        _templateRepo.Update(template);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task<List<TemplateVersionDto>> ListVersionsAsync(Guid templateId, CancellationToken ct = default)
    {
        var versions = await _versionRepo.ListAsync(v => v.TemplateId == templateId, ct);
        return versions
            .Select(v => new TemplateVersionDto(
                v.Id, v.TemplateId, v.Version, v.StorageKey, v.Status.Name, v.FileFormat?.Name,
                v.DataSchema, v.SamplePayload, v.MappingsSnapshot, v.CommitMessage, v.CreatedBy,
                v.CreatedAt, v.UpdatedAt))
            .OrderByDescending(v => v.Version)
            .ToList();
    }

    public async Task<int> RollbackVersionAsync(Guid templateId, int targetVersion, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{templateId}' not found.");

        var currentVersion = await GetCurrentVersionAsync(template, ct);

        var archived = await _versionRepo.FirstOrDefaultAsync(
            v => v.TemplateId == templateId && v.Version == targetVersion, ct)
            ?? throw new NotFoundException($"Version {targetVersion} not found for template '{templateId}'.");

        using var archivedStream = await _storageService.DownloadAsync(StorageBuckets.Templates, archived.StorageKey, ct);
        using var memoryStream = new MemoryStream();
        await archivedStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        int nextVersionNumber = currentVersion.Version + 1;
        
        string ext = archived.FileFormat?.Extension ?? ".html";
        string contentType = archived.FileFormat?.MimeType ?? "text/html; charset=utf-8";

        string versionedKey = $"templates/archive/{template.Slug}_v{nextVersionNumber}{ext}";

        await _storageService.UploadAsync(StorageBuckets.Templates, versionedKey, memoryStream, contentType, ct);

        var newVersion = new TemplateVersion(template.Id, nextVersionNumber, versionedKey, archived.FileFormat, _executionContext.CallerApp ?? "system", $"Rollback to v{targetVersion}");
        newVersion.Publish();
        await _versionRepo.AddAsync(newVersion, ct);

        template.SetCurrentVersion(newVersion.Id);
        _templateRepo.Update(template);

        await _unitOfWork.CommitAsync(ct);
        return nextVersionNumber;
    }

    public async Task<List<string>> ScanPlaceholdersFromStreamAsync(Stream stream, string fileExtension, CancellationToken ct = default)
        => await _scanner.ScanPlaceholdersAsync(stream, fileExtension, ct);

    public async Task<List<string>> ScanPlaceholdersAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Template '{id}' not found.");

        var version = await GetCurrentVersionAsync(template, ct);
        using var stream = await _storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        string ext = Path.GetExtension(version.StorageKey).ToLowerInvariant();
        return await _scanner.ScanPlaceholdersAsync(stream, ext, ct);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Template '{id}' not found.");

        var version = await GetCurrentVersionAsync(template, ct);
        var stream = await _storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        string ext = Path.GetExtension(version.StorageKey).ToLowerInvariant();
        string contentType = ext switch
        {
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "text/html; charset=utf-8"
        };
        return (stream, contentType, $"{template.Slug}{ext}");
    }

    private async Task<TemplateVersion> GetCurrentVersionAsync(Template template, CancellationToken ct)
    {
        if (template.CurrentVersionId is null)
            throw new InvalidOperationException($"Template '{template.Id}' has no published version.");

        return await _versionRepo.GetByIdAsync(template.CurrentVersionId.Value, ct)
            ?? throw new InvalidOperationException($"Current version record for template '{template.Id}' not found.");
    }
}
