using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmkDocServer.Domain.Entities;
using SmkDocServer.Domain.Interfaces;
using SmkDocServer.Infrastructure.Data;
using SmkDocServer.Infrastructure.Services.Storage;

namespace SmkDocServer.Application.Services;

/// <summary>
/// Resolves template file paths and metadata for multi-tenant scoping.
/// Falls back to MinIO download if a template is missing from local disk.
/// </summary>
public class TemplateResolverService : ITemplateResolverService
{
    private readonly string _templateDirectory;
    private readonly AppDbContext _dbContext;
    private readonly IMinioStorageService _minioStorage;
    private readonly MinioSettings _settings;
    private readonly ILogger<TemplateResolverService> _logger;

    public TemplateResolverService(
        IWebHostEnvironment env,
        AppDbContext dbContext,
        IMinioStorageService minioStorage,
        IOptions<MinioSettings> settings,
        ILogger<TemplateResolverService> logger)
    {
        _templateDirectory = Path.Combine(env.ContentRootPath, "Templates");
        if (!Directory.Exists(_templateDirectory))
            Directory.CreateDirectory(_templateDirectory);

        _dbContext = dbContext;
        _minioStorage = minioStorage;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TemplateMetadata>> ListTemplatesAsync(Guid? projectId = null)
    {
        try
        {
            var query = _dbContext.Templates
                .Include(t => t.Project)
                .Include(t => t.Versions)
                .AsSplitQuery()
                .AsNoTracking()
                .AsQueryable();

            if (projectId.HasValue)
            {
                query = query.Where(t => t.ProjectId == projectId.Value || t.IsGlobal);
            }

            var dbTemplates = await query
                .OrderByDescending(t => t.UploadedAt)
                .ToListAsync();

            if (dbTemplates.Any())
                return dbTemplates;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch templates from DB. Falling back to local disk.");
        }

        // Fallback to local disk scan
        var results = new List<TemplateMetadata>();
        if (!Directory.Exists(_templateDirectory)) return results;

        var files = Directory.GetFiles(_templateDirectory, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase));

        foreach (var f in files)
        {
            string rel = Path.GetRelativePath(_templateDirectory, f).Replace('\\', '/');
            if (rel.Contains("/versions/")) continue;
            
            results.Add(new TemplateMetadata
            {
                Id = Guid.Empty,
                FileName = Path.GetFileName(f),
                OriginalName = Path.GetFileName(f),
                Format = Path.GetExtension(f).ToLower(),
                StoragePath = rel,
                IsGlobal = rel.StartsWith("GLOBAL/", StringComparison.OrdinalIgnoreCase) || !rel.Contains('/'),
                UploadedAt = File.GetLastWriteTimeUtc(f)
            });
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<string?> ResolveTemplatePathAsync(string fileName, Guid? projectId = null)
    {
        Project? project = null;
        if (projectId.HasValue)
        {
            project = await _dbContext.Projects.FindAsync(projectId.Value);
        }

        var candidates = new List<string>();
        if (project != null && !string.IsNullOrWhiteSpace(project.Code))
        {
            candidates.Add(Path.Combine(project.Code, fileName));
        }
        candidates.Add(Path.Combine("GLOBAL", fileName));
        candidates.Add(fileName);

        foreach (var relPath in candidates)
        {
            string localPath = Path.Combine(_templateDirectory, relPath);
            if (File.Exists(localPath)) return localPath;

            string s3Path = relPath.Replace('\\', '/');
            try
            {
                if (await _minioStorage.FileExistsAsync(_settings.TemplateBucket, s3Path))
                {
                    string? dir = Path.GetDirectoryName(localPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    using var s3Stream = await _minioStorage.DownloadFileAsync(_settings.TemplateBucket, s3Path);
                    using var fileStream = File.Create(localPath);
                    await s3Stream.CopyToAsync(fileStream);
                    return localPath;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error downloading template from MinIO: {S3Path}", s3Path);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<TemplateMetadata?> GetMetadataAsync(string fileName, Guid? projectId = null)
    {
        return await _dbContext.Templates
            .Include(t => t.Project)
            .Include(t => t.FieldMappings)
            .AsNoTracking()
            .OrderByDescending(t => projectId.HasValue ? (t.ProjectId == projectId) : t.IsGlobal)
            .FirstOrDefaultAsync(t => t.FileName == fileName && (t.IsGlobal || t.ProjectId == projectId));
    }

}
