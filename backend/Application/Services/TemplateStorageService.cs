using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmkDocServer.Domain.Entities;
using SmkDocServer.Domain.Interfaces;
using SmkDocServer.Infrastructure.Data;
using SmkDocServer.Infrastructure.Services.Storage;

namespace SmkDocServer.Application.Services;

/// <summary>
/// Handles all file I/O operations for templates:
/// saving to local disk, uploading to MinIO, deleting, and startup sync.
/// SRP: This class is responsible ONLY for template file storage — not versioning, not schema, not resolution.
/// </summary>
public class TemplateStorageService : ITemplateStorageService
{
    private readonly string _templateDirectory;
    private readonly AppDbContext _dbContext;
    private readonly IMinioStorageService _minioStorage;
    private readonly MinioSettings _settings;
    private readonly ILogger<TemplateStorageService> _logger;

    public TemplateStorageService(
        IWebHostEnvironment env,
        AppDbContext dbContext,
        IMinioStorageService minioStorage,
        IOptions<MinioSettings> settings,
        ILogger<TemplateStorageService> logger)
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
    public async Task<TemplateMetadata> SaveTemplateAsync(IFormFile file, Guid? projectId = null, bool isGlobal = false)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("No file was uploaded.");

        string extension = Path.GetExtension(file.FileName).ToLower();
        if (extension != ".docx" && extension != ".xlsx")
            throw new ArgumentException("Only .docx and .xlsx files are allowed.");

        string contentType = extension == ".docx"
            ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        // Determine project scope folder
        string scopeFolder = "GLOBAL";
        Project? project = null;
        if (!isGlobal && projectId.HasValue)
        {
            project = await _dbContext.Projects.FindAsync(projectId.Value);
            if (project != null) scopeFolder = project.Code;
        }

        string storagePath = $"{scopeFolder}/{file.FileName}";
        string targetDir = Path.Combine(_templateDirectory, scopeFolder);
        Directory.CreateDirectory(targetDir);
        string localFilePath = Path.Combine(targetDir, file.FileName);

        // Archive existing version before overwriting
        TemplateMetadata? metadata = null;
        try
        {
            metadata = await _dbContext.Templates
                .Include(t => t.Versions)
                .FirstOrDefaultAsync(t =>
                    t.FileName == file.FileName &&
                    (isGlobal ? t.IsGlobal : t.ProjectId == projectId));

            if (metadata != null && File.Exists(localFilePath))
            {
                try
                {
                    int oldVer = metadata.CurrentVersion;
                    string verScope = $"versions/{scopeFolder}";
                    string verDir = Path.Combine(_templateDirectory, verScope);
                    Directory.CreateDirectory(verDir);

                    string baseName = Path.GetFileNameWithoutExtension(file.FileName);
                    string verFileName = $"{baseName}_v{oldVer}{extension}";
                    string verLocalPath = Path.Combine(verDir, verFileName);
                    string verStoragePath = $"{verScope}/{verFileName}";

                    // Backup old file to disk
                    File.Copy(localFilePath, verLocalPath, true);

                    // Backup to MinIO
                    try
                    {
                        using var verStream = File.OpenRead(verLocalPath);
                        await _minioStorage.UploadFileAsync(_settings.TemplateBucket, verStoragePath, verStream, contentType);
                    }
                    catch (Exception s3Ex)
                    {
                        _logger.LogWarning(s3Ex, "Could not upload version archive to MinIO for '{FileName}'", file.FileName);
                    }

                    // Record version in DB
                    var existingOldVer = metadata.Versions.FirstOrDefault(v => v.VersionNumber == oldVer);
                    if (existingOldVer != null)
                    {
                        existingOldVer.StoragePath = verStoragePath;
                    }
                    else
                    {
                        var oldFileInfo = new FileInfo(verLocalPath);
                        _dbContext.TemplateVersions.Add(new TemplateVersion
                        {
                            TemplateId = metadata.Id,
                            VersionNumber = oldVer,
                            StoragePath = verStoragePath,
                            FileSizeBytes = oldFileInfo.Length,
                            UploadedAt = metadata.UploadedAt,
                            Note = $"เวอร์ชัน {oldVer} (สำรองก่อนอัปเดต)"
                        });
                    }
                    metadata.CurrentVersion = oldVer + 1;
                }
                catch (Exception archiveEx)
                {
                    _logger.LogWarning(archiveEx, "Failed to archive previous version of '{FileName}'", file.FileName);
                }
            }
        }
        catch (Exception dbCheckEx)
        {
            _logger.LogWarning(dbCheckEx, "Failed to check existing template in database for versioning.");
        }

        // 1. Save new file to local disk
        using (var stream = new FileStream(localFilePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        int activeVer = metadata?.CurrentVersion ?? 1;
        string newVerScope = $"versions/{scopeFolder}";
        string newVerFileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_v{activeVer}{extension}";
        string newVerStoragePath = $"{newVerScope}/{newVerFileName}";
        string newVerLocalPath = Path.Combine(_templateDirectory, newVerStoragePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(newVerLocalPath)!);
        File.Copy(localFilePath, newVerLocalPath, true);

        // 2. Upload to MinIO (active file + version archive via server-side copy)
        try
        {
            await _minioStorage.EnsureBucketExistsAsync(_settings.TemplateBucket);
            using (var readStream = File.OpenRead(localFilePath))
            {
                await _minioStorage.UploadFileAsync(_settings.TemplateBucket, storagePath, readStream, contentType);
            }

            try
            {
                await _minioStorage.CopyObjectAsync(_settings.TemplateBucket, storagePath, _settings.TemplateBucket, newVerStoragePath);
            }
            catch (Exception cpEx)
            {
                _logger.LogWarning(cpEx, "Server-side copy failed for '{Archive}', falling back to direct upload", newVerStoragePath);
                using var archiveStream = File.OpenRead(newVerLocalPath);
                await _minioStorage.UploadFileAsync(_settings.TemplateBucket, newVerStoragePath, archiveStream, contentType);
            }

            _logger.LogInformation("Stored template '{StoragePath}' and archive '{Archive}' in MinIO", storagePath, newVerStoragePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upload template '{StoragePath}' to MinIO. Saved locally only.", storagePath);
        }

        // 3. Save/Update metadata in PostgreSQL
        try
        {
            var newFileInfo = new FileInfo(localFilePath);

            if (metadata != null)
            {
                metadata.OriginalName = file.FileName;
                metadata.Format = extension;
                metadata.StoragePath = storagePath;
                metadata.IsGlobal = isGlobal;
                metadata.UploadedAt = DateTime.UtcNow;

                _dbContext.TemplateVersions.Add(new TemplateVersion
                {
                    TemplateId = metadata.Id,
                    VersionNumber = metadata.CurrentVersion,
                    StoragePath = newVerStoragePath,
                    FileSizeBytes = newFileInfo.Length,
                    UploadedAt = DateTime.UtcNow,
                    Note = $"เวอร์ชัน {metadata.CurrentVersion} (อัปเดตล่าสุด)"
                });
            }
            else
            {
                metadata = new TemplateMetadata
                {
                    Id = Guid.NewGuid(),
                    ProjectId = isGlobal ? null : projectId,
                    FileName = file.FileName,
                    OriginalName = file.FileName,
                    Format = extension,
                    StoragePath = storagePath,
                    IsGlobal = isGlobal,
                    CurrentVersion = 1,
                    UploadedAt = DateTime.UtcNow
                };
                _dbContext.Templates.Add(metadata);

                _dbContext.TemplateVersions.Add(new TemplateVersion
                {
                    TemplateId = metadata.Id,
                    VersionNumber = 1,
                    StoragePath = newVerStoragePath,
                    FileSizeBytes = newFileInfo.Length,
                    UploadedAt = DateTime.UtcNow,
                    Note = "เวอร์ชัน 1 (สร้างครั้งแรก)"
                });
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Saved template '{FileName}' v{Ver} to PostgreSQL", file.FileName, metadata.CurrentVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record metadata for '{FileName}' in database.", file.FileName);
        }

        return metadata ?? new TemplateMetadata
        {
            FileName = file.FileName,
            OriginalName = file.FileName,
            Format = extension,
            StoragePath = storagePath,
            IsGlobal = isGlobal,
            CurrentVersion = 1
        };
    }

    /// <inheritdoc />
    public async Task<TemplateMetadata> SaveHtmlTemplateAsync(string fileName, string htmlContent, Guid? projectId = null, bool isGlobal = false)
    {
        if (string.IsNullOrWhiteSpace(htmlContent))
            throw new ArgumentException("HTML content must not be empty.");

        if (!fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            fileName += ".html";

        string scopeFolder = "GLOBAL";
        Project? project = null;
        if (!isGlobal && projectId.HasValue)
        {
            project = await _dbContext.Projects.FindAsync(projectId.Value);
            if (project != null) scopeFolder = project.Code;
        }

        string storagePath = $"{scopeFolder}/{fileName}";
        string targetDir = Path.Combine(_templateDirectory, scopeFolder);
        Directory.CreateDirectory(targetDir);
        string localFilePath = Path.Combine(targetDir, fileName);

        byte[] htmlBytes = System.Text.Encoding.UTF8.GetBytes(htmlContent);

        // Save to local disk
        await File.WriteAllBytesAsync(localFilePath, htmlBytes);

        // Upload to MinIO
        try
        {
            await _minioStorage.EnsureBucketExistsAsync(_settings.TemplateBucket);
            using var readStream = new MemoryStream(htmlBytes);
            await _minioStorage.UploadFileAsync(_settings.TemplateBucket, storagePath, readStream, "text/html; charset=utf-8");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upload HTML template '{StoragePath}' to MinIO. Saved locally only.", storagePath);
        }

        // Save/Update metadata in PostgreSQL
        var metadata = await _dbContext.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t =>
                t.FileName == fileName &&
                (isGlobal ? t.IsGlobal : t.ProjectId == projectId));

        string verScope = $"versions/{scopeFolder}";
        string verDir = Path.Combine(_templateDirectory, verScope);
        Directory.CreateDirectory(verDir);

        if (metadata != null)
        {
            int oldVer = metadata.CurrentVersion;
            metadata.CurrentVersion = oldVer + 1;
            metadata.UploadedAt = DateTime.UtcNow;
            metadata.EngineType = SmkDocServer.Domain.Models.TemplateEngineType.Tiptap;

            string verFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_v{oldVer}.html";
            string verStoragePath = $"{verScope}/{verFileName}";
            string verLocalPath = Path.Combine(verDir, verFileName);
            File.Copy(localFilePath, verLocalPath, true);

            _dbContext.TemplateVersions.Add(new TemplateVersion
            {
                TemplateId = metadata.Id,
                VersionNumber = metadata.CurrentVersion,
                StoragePath = $"{verScope}/{Path.GetFileNameWithoutExtension(fileName)}_v{metadata.CurrentVersion}.html",
                FileSizeBytes = htmlBytes.Length,
                UploadedAt = DateTime.UtcNow,
                Note = $"เวอร์ชัน {metadata.CurrentVersion} (อัปเดตล่าสุด)"
            });
        }
        else
        {
            string verFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_v1.html";
            string verLocalPath = Path.Combine(verDir, verFileName);
            File.Copy(localFilePath, verLocalPath, true);

            metadata = new TemplateMetadata
            {
                Id = Guid.NewGuid(),
                ProjectId = isGlobal ? null : projectId,
                FileName = fileName,
                OriginalName = fileName,
                Format = ".html",
                StoragePath = storagePath,
                IsGlobal = isGlobal,
                CurrentVersion = 1,
                EngineType = SmkDocServer.Domain.Models.TemplateEngineType.Tiptap,
                UploadedAt = DateTime.UtcNow
            };
            _dbContext.Templates.Add(metadata);

            _dbContext.TemplateVersions.Add(new TemplateVersion
            {
                TemplateId = metadata.Id,
                VersionNumber = 1,
                StoragePath = $"{verScope}/{verFileName}",
                FileSizeBytes = htmlBytes.Length,
                UploadedAt = DateTime.UtcNow,
                Note = "เวอร์ชัน 1 (สร้างครั้งแรก)"
            });
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Saved HTML template '{FileName}' v{Ver} to PostgreSQL", fileName, metadata.CurrentVersion);

        return metadata;
    }

    /// <inheritdoc />
    public async Task DeleteTemplateAsync(string fileName, Guid? projectId = null)
    {
        var template = await _dbContext.Templates
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.FileName == fileName &&
                (projectId.HasValue ? (t.ProjectId == projectId || t.IsGlobal) : true));

        if (template == null)
            throw new FileNotFoundException($"Template '{fileName}' not found.");

        string storagePath = template.StoragePath;
        _dbContext.Templates.Remove(template);
        await _dbContext.SaveChangesAsync();

        // Delete from local disk
        string localPath = Path.Combine(_templateDirectory, storagePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(localPath)) File.Delete(localPath);

        string fallbackFlat = Path.Combine(_templateDirectory, fileName);
        if (File.Exists(fallbackFlat)) File.Delete(fallbackFlat);

        // Delete from MinIO
        try
        {
            if (await _minioStorage.FileExistsAsync(_settings.TemplateBucket, storagePath))
                await _minioStorage.DeleteFileAsync(_settings.TemplateBucket, storagePath);

            if (storagePath != fileName && await _minioStorage.FileExistsAsync(_settings.TemplateBucket, fileName))
                await _minioStorage.DeleteFileAsync(_settings.TemplateBucket, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting template '{StoragePath}' from MinIO", storagePath);
        }
    }

    /// <inheritdoc />
    public async Task<string> EnsureLocalFileAsync(string storagePath, string localPath)
    {
        if (File.Exists(localPath)) return localPath;

        string? dir = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var s3Stream = await _minioStorage.DownloadFileAsync(_settings.TemplateBucket, storagePath);
        using var fileStream = File.Create(localPath);
        await s3Stream.CopyToAsync(fileStream);

        return localPath;
    }

    /// <inheritdoc />
    public async Task<TemplateMetadata> SaveJsonTemplateAsync(string fileName, string jsonContent, Guid? projectId = null, bool isGlobal = false)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
            throw new ArgumentException("JSON content must not be empty.");

        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            fileName += ".json";

        string scopeFolder = "GLOBAL";
        if (!isGlobal && projectId.HasValue)
        {
            var project = await _dbContext.Projects.FindAsync(projectId.Value);
            if (project != null) scopeFolder = project.Code;
        }

        string storagePath   = $"{scopeFolder}/{fileName}";
        string targetDir     = Path.Combine(_templateDirectory, scopeFolder);
        Directory.CreateDirectory(targetDir);
        string localFilePath = Path.Combine(targetDir, fileName);

        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(jsonContent);
        await File.WriteAllBytesAsync(localFilePath, jsonBytes);

        try
        {
            await _minioStorage.EnsureBucketExistsAsync(_settings.TemplateBucket);
            using var readStream = new MemoryStream(jsonBytes);
            await _minioStorage.UploadFileAsync(_settings.TemplateBucket, storagePath, readStream, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to upload JSON template '{StoragePath}' to MinIO. Saved locally only.", storagePath);
        }

        var metadata = await _dbContext.Templates
            .Include(t => t.Versions)
            .FirstOrDefaultAsync(t =>
                t.FileName == fileName &&
                (isGlobal ? t.IsGlobal : t.ProjectId == projectId));

        string verScope = $"versions/{scopeFolder}";
        string verDir   = Path.Combine(_templateDirectory, verScope);
        Directory.CreateDirectory(verDir);

        if (metadata != null)
        {
            int oldVer = metadata.CurrentVersion;
            metadata.CurrentVersion = oldVer + 1;
            metadata.UploadedAt     = DateTime.UtcNow;
            metadata.EngineType     = SmkDocServer.Domain.Models.TemplateEngineType.ReportBro;

            string verFileName  = $"{Path.GetFileNameWithoutExtension(fileName)}_v{oldVer}.json";
            string verLocalPath = Path.Combine(verDir, verFileName);
            File.Copy(localFilePath, verLocalPath, true);

            _dbContext.TemplateVersions.Add(new TemplateVersion
            {
                TemplateId    = metadata.Id,
                VersionNumber = metadata.CurrentVersion,
                StoragePath   = $"{verScope}/{Path.GetFileNameWithoutExtension(fileName)}_v{metadata.CurrentVersion}.json",
                FileSizeBytes = jsonBytes.Length,
                UploadedAt    = DateTime.UtcNow,
                Note          = $"เวอร์ชัน {metadata.CurrentVersion} (อัปเดตล่าสุด)"
            });
        }
        else
        {
            string verFileName  = $"{Path.GetFileNameWithoutExtension(fileName)}_v1.json";
            string verLocalPath = Path.Combine(verDir, verFileName);
            File.Copy(localFilePath, verLocalPath, true);

            metadata = new TemplateMetadata
            {
                Id             = Guid.NewGuid(),
                ProjectId      = isGlobal ? null : projectId,
                FileName       = fileName,
                OriginalName   = fileName,
                Format         = ".json",
                StoragePath    = storagePath,
                IsGlobal       = isGlobal,
                CurrentVersion = 1,
                EngineType     = SmkDocServer.Domain.Models.TemplateEngineType.ReportBro,
                UploadedAt     = DateTime.UtcNow
            };
            _dbContext.Templates.Add(metadata);

            _dbContext.TemplateVersions.Add(new TemplateVersion
            {
                TemplateId    = metadata.Id,
                VersionNumber = 1,
                StoragePath   = $"{verScope}/{verFileName}",
                FileSizeBytes = jsonBytes.Length,
                UploadedAt    = DateTime.UtcNow,
                Note          = "เวอร์ชัน 1 (สร้างครั้งแรก)"
            });
        }

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Saved ReportBro JSON template '{FileName}' v{Ver} to PostgreSQL", fileName, metadata.CurrentVersion);

        return metadata;
    }

    public async Task SyncLocalTemplatesAsync()
    {
        if (!Directory.Exists(_templateDirectory)) return;

        try
        {
            await _minioStorage.EnsureBucketExistsAsync(_settings.TemplateBucket);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not ensure MinIO bucket '{Bucket}' exists.", _settings.TemplateBucket);
        }

        var files = Directory.GetFiles(_templateDirectory, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
                     && !f.Replace('\\', '/').Contains("/versions/"))
            .ToList();

        foreach (var filePath in files)
        {
            var fileName = Path.GetFileName(filePath);
            var extension = Path.GetExtension(filePath).ToLower();
            var contentType = extension == ".docx"
                ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

            string relPath = Path.GetRelativePath(_templateDirectory, filePath).Replace('\\', '/');
            string[] parts = relPath.Split('/');
            string scopeFolder = parts.Length > 1 ? parts[0] : "GLOBAL";
            bool isGlobal = scopeFolder.Equals("GLOBAL", StringComparison.OrdinalIgnoreCase) || parts.Length == 1;

            // Sync to MinIO
            try
            {
                bool inMinio = await _minioStorage.FileExistsAsync(_settings.TemplateBucket, relPath);
                if (!inMinio)
                {
                    using var fs = File.OpenRead(filePath);
                    await _minioStorage.UploadFileAsync(_settings.TemplateBucket, relPath, fs, contentType);
                    _logger.LogInformation("Synced local template '{RelPath}' to MinIO", relPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not sync '{RelPath}' to MinIO", relPath);
            }

            // Sync to PostgreSQL
            try
            {
                var existing = await _dbContext.Templates
                    .Include(t => t.Versions)
                    .FirstOrDefaultAsync(t => t.FileName == fileName && t.StoragePath == relPath);

                if (existing == null)
                {
                    string verScope = $"versions/{scopeFolder}";
                    string baseName = Path.GetFileNameWithoutExtension(fileName);
                    string verStoragePath = $"{verScope}/{baseName}_v1{extension}";
                    string verLocalPath = Path.Combine(_templateDirectory, verStoragePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(verLocalPath)!);
                    if (!File.Exists(verLocalPath)) File.Copy(filePath, verLocalPath, true);

                    try
                    {
                        if (!await _minioStorage.FileExistsAsync(_settings.TemplateBucket, verStoragePath))
                        {
                            using var vfs = File.OpenRead(verLocalPath);
                            await _minioStorage.UploadFileAsync(_settings.TemplateBucket, verStoragePath, vfs, contentType);
                        }
                    }
                    catch { }

                    var newTemplate = new TemplateMetadata
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = null,
                        FileName = fileName,
                        OriginalName = fileName,
                        Format = extension,
                        StoragePath = relPath,
                        IsGlobal = isGlobal,
                        CurrentVersion = 1,
                        UploadedAt = File.GetLastWriteTimeUtc(filePath)
                    };
                    _dbContext.Templates.Add(newTemplate);

                    _dbContext.TemplateVersions.Add(new TemplateVersion
                    {
                        TemplateId = newTemplate.Id,
                        VersionNumber = 1,
                        StoragePath = verStoragePath,
                        FileSizeBytes = new FileInfo(filePath).Length,
                        UploadedAt = newTemplate.UploadedAt,
                        Note = "เวอร์ชัน 1 (ต้นฉบับ)"
                    });

                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Synced local template '{FileName}' to PostgreSQL", fileName);
                }
                else if (existing.Versions == null || !existing.Versions.Any())
                {
                    string verScope = $"versions/{scopeFolder}";
                    string baseName = Path.GetFileNameWithoutExtension(fileName);
                    string verStoragePath = $"{verScope}/{baseName}_v1{extension}";
                    string verLocalPath = Path.Combine(_templateDirectory, verStoragePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(verLocalPath)!);
                    if (!File.Exists(verLocalPath)) File.Copy(filePath, verLocalPath, true);

                    _dbContext.TemplateVersions.Add(new TemplateVersion
                    {
                        TemplateId = existing.Id,
                        VersionNumber = 1,
                        StoragePath = verStoragePath,
                        FileSizeBytes = new FileInfo(filePath).Length,
                        UploadedAt = existing.UploadedAt,
                        Note = "เวอร์ชัน 1 (ต้นฉบับ)"
                    });
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not sync '{FileName}' to database", fileName);
            }
        }
    }
}
