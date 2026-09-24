using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmkDocServer.Domain.Entities;
using SmkDocServer.Domain.Interfaces;
using SmkDocServer.Infrastructure.Data;
using SmkDocServer.Infrastructure.Services.Storage;

namespace SmkDocServer.Application.Services;

/// <summary>
/// Handles template version history: listing versions and rolling back.
/// SRP: This class is responsible ONLY for version management — not storage, not schema.
/// </summary>
public class TemplateVersioningService : ITemplateVersioningService
{
    private readonly string _templateDirectory;
    private readonly AppDbContext _dbContext;
    private readonly IMinioStorageService _minioStorage;
    private readonly MinioSettings _settings;
    private readonly ILogger<TemplateVersioningService> _logger;

    public TemplateVersioningService(
        IWebHostEnvironment env,
        AppDbContext dbContext,
        IMinioStorageService minioStorage,
        IOptions<MinioSettings> settings,
        ILogger<TemplateVersioningService> logger)
    {
        _templateDirectory = Path.Combine(env.ContentRootPath, "Templates");
        _dbContext = dbContext;
        _minioStorage = minioStorage;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<TemplateVersion>> GetVersionsAsync(string fileName, Guid? projectId = null)
    {
        var template = await _dbContext.Templates
            .Include(t => t.Versions)
            .Include(t => t.Project)
            .AsNoTracking()
            .OrderByDescending(t => projectId.HasValue ? (t.ProjectId == projectId) : t.IsGlobal)
            .FirstOrDefaultAsync(t => t.FileName == fileName && (t.IsGlobal || t.ProjectId == projectId));

        if (template == null)
            return Enumerable.Empty<TemplateVersion>();

        if (template.Versions == null || !template.Versions.Any())
        {
            string localPath = Path.Combine(_templateDirectory, template.StoragePath.Replace('/', Path.DirectorySeparatorChar));
            long size = File.Exists(localPath) ? new FileInfo(localPath).Length : 0;

            return new List<TemplateVersion>
            {
                new TemplateVersion
                {
                    TemplateId = template.Id,
                    VersionNumber = 1,
                    StoragePath = template.StoragePath,
                    FileSizeBytes = size,
                    UploadedAt = template.UploadedAt,
                    Note = "เวอร์ชัน 1 (ต้นฉบับ)"
                }
            };
        }

        return template.Versions.OrderByDescending(v => v.VersionNumber).ToList();
    }

    /// <inheritdoc />
    public async Task<TemplateMetadata> RollbackAsync(string fileName, int targetVersion, Guid? projectId = null)
    {
        var template = await _dbContext.Templates
            .Include(t => t.Versions)
            .Include(t => t.Project)
            .OrderByDescending(t => projectId.HasValue ? (t.ProjectId == projectId) : t.IsGlobal)
            .FirstOrDefaultAsync(t => t.FileName == fileName && (t.IsGlobal || t.ProjectId == projectId));

        if (template == null)
            throw new FileNotFoundException($"ไม่พบแม่แบบ '{fileName}' ในระบบ");

        if (targetVersion == template.CurrentVersion)
            throw new InvalidOperationException($"แม่แบบนี้เป็นเวอร์ชัน {targetVersion} ซึ่งเป็นเวอร์ชันปัจจุบันอยู่แล้ว");

        string scopeFolder = template.IsGlobal ? "GLOBAL" : (template.Project?.Code ?? "GLOBAL");
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName).ToLower();
        string contentType = extension == ".docx"
            ? "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        // 1. Locate the target version file
        string? targetLocalPath = null;
        var targetVerRecord = template.Versions?.FirstOrDefault(v => v.VersionNumber == targetVersion);
        if (targetVerRecord != null)
        {
            string candidatePath = Path.Combine(_templateDirectory, targetVerRecord.StoragePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidatePath))
            {
                targetLocalPath = candidatePath;
            }
            else
            {
                // Try to download from MinIO
                try
                {
                    string? dir = Path.GetDirectoryName(candidatePath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    using var s3Stream = await _minioStorage.DownloadFileAsync(_settings.TemplateBucket, targetVerRecord.StoragePath);
                    using var localStream = File.Create(candidatePath);
                    await s3Stream.CopyToAsync(localStream);
                    targetLocalPath = candidatePath;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not download version {Ver} for '{FileName}' from MinIO", targetVersion, fileName);
                }
            }
        }

        if (string.IsNullOrEmpty(targetLocalPath))
            throw new FileNotFoundException($"ไม่พบไฟล์ของเวอร์ชัน {targetVersion} สำหรับแม่แบบ '{fileName}'");

        // 2. Archive current version
        string activePath = Path.Combine(_templateDirectory, template.StoragePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(activePath))
        {
            int curVer = template.CurrentVersion;
            string archiveScope = $"versions/{scopeFolder}";
            string archiveFileName = $"{baseName}_v{curVer}{extension}";
            string archiveLocalPath = Path.Combine(_templateDirectory, archiveScope.Replace('/', Path.DirectorySeparatorChar), archiveFileName);
            string archiveStoragePath = $"{archiveScope}/{archiveFileName}";
            Directory.CreateDirectory(Path.GetDirectoryName(archiveLocalPath)!);
            File.Copy(activePath, archiveLocalPath, true);

            try
            {
                await _minioStorage.CopyObjectAsync(_settings.TemplateBucket, template.StoragePath, _settings.TemplateBucket, archiveStoragePath);
            }
            catch
            {
                using var archStream = File.OpenRead(archiveLocalPath);
                await _minioStorage.UploadFileAsync(_settings.TemplateBucket, archiveStoragePath, archStream, contentType);
            }

            _dbContext.TemplateVersions.Add(new TemplateVersion
            {
                TemplateId = template.Id,
                VersionNumber = curVer,
                StoragePath = archiveStoragePath,
                FileSizeBytes = new FileInfo(archiveLocalPath).Length,
                UploadedAt = DateTime.UtcNow,
                Note = $"เวอร์ชัน {curVer} (สำรองก่อน Rollback)"
            });
        }

        // 3. Restore the target version as active
        File.Copy(targetLocalPath, activePath, true);

        using (var readStream = File.OpenRead(activePath))
        {
            await _minioStorage.UploadFileAsync(_settings.TemplateBucket, template.StoragePath, readStream, contentType);
        }

        template.CurrentVersion = template.CurrentVersion + 1;
        template.UploadedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Rolled back '{FileName}' to version {TargetVer}, new version is {NewVer}",
            fileName, targetVersion, template.CurrentVersion);

        return template;
    }
}
