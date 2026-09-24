using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.UseCases.Templates;

public class TemplateManagementUseCase
{
    private readonly IRepository<Template> _templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo;
    private readonly IStorageService _storageService;
    private readonly ITemplateScannerService _scanner;
    private readonly IDocxSecurityScanner _securityScanner;
    private readonly IExecutionContext _executionContext;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Project>? _projectRepo;

    public TemplateManagementUseCase(
        IRepository<Template> templateRepo,
        IRepository<TemplateVersion> versionRepo,
        IStorageService storageService,
        ITemplateScannerService scanner,
        IDocxSecurityScanner securityScanner,
        IExecutionContext executionContext,
        IUnitOfWork unitOfWork,
        IRepository<Project>? projectRepo = null)
    {
        _templateRepo = templateRepo;
        _versionRepo = versionRepo;
        _storageService = storageService;
        _scanner = scanner;
        _securityScanner = securityScanner;
        _executionContext = executionContext;
        _unitOfWork = unitOfWork;
        _projectRepo = projectRepo;
    }

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
                t.CreatedAt, t.UpdatedAt))
            .ToList();
    }

    public async Task<TemplateDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Template with ID '{id}' not found.");

        TemplateFormat? format = null;
        if (t.CurrentVersionId.HasValue)
        {
            var version = await _versionRepo.GetByIdAsync(t.CurrentVersionId.Value, ct);
            format = version?.FileFormat;
        }

        return new TemplateDto(t.Id, t.Name, t.Slug, t.Category, t.IsActive,
            t.CurrentVersionId, format, t.CreatedAt, t.UpdatedAt);
    }

    public async Task<TemplateDto> CreateTemplateAsync(
        CreateTemplateRequest request,
        Stream? fileStream = null,
        string? fileName = null,
        CancellationToken ct = default)
    {
        var existing = await _templateRepo.FirstOrDefaultAsync(t => t.Slug == request.Slug, ct);
        if (existing != null)
            throw new InvalidOperationException($"Template slug '{request.Slug}' is already in use.");

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
            string contentType = ext switch
            {
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _       => "text/html; charset=utf-8"
            };
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

        var template = new Template
        {
            ProjectId = targetProjectId,
            Name = request.Name,
            Slug = request.Slug,
            Category = request.Category,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        await _templateRepo.AddAsync(template, ct);
        await _unitOfWork.SaveChangesAsync(ct); // Save template first to generate ID and break circular dependency

        var initialVersion = new TemplateVersion
        {
            TemplateId = template.Id,
            Version = 1,
            StorageKey = storageKey,
            FileFormat = fileFormat,
            Status = TemplateVersionStatus.Published,
            CommitMessage = "Initial version",
            CreatedBy = _executionContext.CallerApp ?? "system",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _versionRepo.AddAsync(initialVersion, ct);
        await _unitOfWork.SaveChangesAsync(ct); // Save version first to generate ID

        template.CurrentVersionId = initialVersion.Id;
        _templateRepo.Update(template);
        await _unitOfWork.SaveChangesAsync(ct); // Update template with CurrentVersionId

        return new TemplateDto(template.Id, template.Name, template.Slug, template.Category,
            template.IsActive, template.CurrentVersionId, initialVersion.FileFormat, template.CreatedAt, template.UpdatedAt);
    }

    public async Task<string> GetTemplateHtmlAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Template '{id}' not found.");

        var version = await GetCurrentVersionAsync(template, ct);
        
        if (version.FileFormat != null && version.FileFormat != TemplateFormat.Html)
            throw new InvalidOperationException("Only HTML templates can be opened in the HTML source editor.");

        using var stream = await _storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }

    public async Task<int> SaveTemplateHtmlAsync(Guid id, SaveTemplateHtmlRequest request, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Template '{id}' not found.");

        var currentVersion = await GetCurrentVersionAsync(template, ct);
        int nextVersionNumber = currentVersion.Version + 1;

        string versionedKey = $"templates/archive/{template.Slug}_v{nextVersionNumber}.html";
        string activeKey = $"templates/{template.Slug}.html";

        byte[] htmlBytes = Encoding.UTF8.GetBytes(request.Html);

        using (var stream = new MemoryStream(htmlBytes))
            await _storageService.UploadAsync(StorageBuckets.Templates, versionedKey, stream, "text/html; charset=utf-8", ct);

        using (var stream = new MemoryStream(htmlBytes))
            await _storageService.UploadAsync(StorageBuckets.Templates, activeKey, stream, "text/html; charset=utf-8", ct);

        var newVersion = new TemplateVersion
        {
            TemplateId = template.Id,
            Version = nextVersionNumber,
            StorageKey = versionedKey,
            FileFormat = TemplateFormat.Html,
            Status = TemplateVersionStatus.Published,
            CommitMessage = request.ChangeNote,
            CreatedBy = _executionContext.CallerApp ?? "developer",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _versionRepo.AddAsync(newVersion, ct);

        template.CurrentVersionId = newVersion.Id;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        _templateRepo.Update(template);

        await _unitOfWork.SaveChangesAsync(ct);
        return nextVersionNumber;
    }

    public async Task UpdateMetadataAsync(Guid id, UpdateTemplateRequest request, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Template '{id}' not found.");

        if (request.Name != null) template.Name = request.Name;
        if (request.Category != null) template.Category = request.Category;
        if (request.IsActive.HasValue) template.IsActive = request.IsActive.Value;
        template.UpdatedAt = DateTimeOffset.UtcNow;

        _templateRepo.Update(template);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeactivateTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Template '{id}' not found.");

        template.IsActive = false;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        _templateRepo.Update(template);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<List<TemplateVersion>> ListVersionsAsync(Guid templateId, CancellationToken ct = default)
    {
        var versions = await _versionRepo.ListAsync(v => v.TemplateId == templateId, ct);
        return versions.OrderByDescending(v => v.Version).ToList();
    }

    public async Task<int> RollbackVersionAsync(Guid templateId, int targetVersion, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new KeyNotFoundException($"Template '{templateId}' not found.");

        var currentVersion = await GetCurrentVersionAsync(template, ct);

        var archived = await _versionRepo.FirstOrDefaultAsync(
            v => v.TemplateId == templateId && v.Version == targetVersion, ct)
            ?? throw new KeyNotFoundException($"Version {targetVersion} not found for template '{templateId}'.");

        using var archivedStream = await _storageService.DownloadAsync(StorageBuckets.Templates, archived.StorageKey, ct);
        using var memoryStream = new MemoryStream();
        await archivedStream.CopyToAsync(memoryStream, ct);
        memoryStream.Position = 0;

        int nextVersionNumber = currentVersion.Version + 1;
        
        string ext = archived.FileFormat switch
        {
            TemplateFormat.Docx => ".docx",
            TemplateFormat.Xlsx => ".xlsx",
            _ => ".html"
        };
        string contentType = archived.FileFormat switch
        {
            TemplateFormat.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            TemplateFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "text/html; charset=utf-8"
        };

        string versionedKey = $"templates/archive/{template.Slug}_v{nextVersionNumber}{ext}";

        await _storageService.UploadAsync(StorageBuckets.Templates, versionedKey, memoryStream, contentType, ct);

        var newVersion = new TemplateVersion
        {
            TemplateId = template.Id,
            Version = nextVersionNumber,
            StorageKey = versionedKey,
            FileFormat = archived.FileFormat,
            Status = TemplateVersionStatus.Published,
            CommitMessage = $"Rollback to v{targetVersion}",
            CreatedBy = _executionContext.CallerApp ?? "system",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _versionRepo.AddAsync(newVersion, ct);

        template.CurrentVersionId = newVersion.Id;
        template.UpdatedAt = DateTimeOffset.UtcNow;
        _templateRepo.Update(template);

        await _unitOfWork.SaveChangesAsync(ct);
        return nextVersionNumber;
    }

    public async Task<List<string>> ScanPlaceholdersFromStreamAsync(Stream stream, string fileExtension, CancellationToken ct = default)
        => await _scanner.ScanPlaceholdersAsync(stream, fileExtension, ct);

    public async Task<List<string>> ScanPlaceholdersAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Template '{id}' not found.");

        var version = await GetCurrentVersionAsync(template, ct);
        using var stream = await _storageService.DownloadAsync(StorageBuckets.Templates, version.StorageKey, ct);
        string ext = Path.GetExtension(version.StorageKey).ToLowerInvariant();
        return await _scanner.ScanPlaceholdersAsync(stream, ext, ct);
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> DownloadTemplateAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Template '{id}' not found.");

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
