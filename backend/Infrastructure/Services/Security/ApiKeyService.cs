using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmkDocServer.Domain.Entities;
using SmkDocServer.Domain.Interfaces;
using SmkDocServer.Infrastructure.Data;

namespace SmkDocServer.Infrastructure.Services.Security;

public class ApiKeyService : IApiKeyService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(AppDbContext dbContext, ILogger<ApiKeyService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<(Project Project, ApiKey ApiKey)?> ValidateApiKeyAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return null;

        var keyEntity = await _dbContext.ApiKeys
            .Include(k => k.Project)
            .FirstOrDefaultAsync(k => k.Key == apiKey && k.IsActive);

        if (keyEntity == null) return null;

        // Check expiration
        if (keyEntity.ExpiresAt.HasValue && keyEntity.ExpiresAt.Value < DateTime.UtcNow)
        {
            return null;
        }

        // Check project active
        if (keyEntity.Project == null || !keyEntity.Project.IsActive)
        {
            return null;
        }

        // Throttle LastUsedAt write: only update when stale by >5 minutes (fire-and-forget, never blocks the request)
        var now = DateTime.UtcNow;
        if (keyEntity.LastUsedAt == null || (now - keyEntity.LastUsedAt.Value).TotalMinutes > 5)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _dbContext.Database.ExecuteSqlRawAsync(
                        "UPDATE \"ApiKeys\" SET \"LastUsedAt\" = {0} WHERE \"Id\" = {1}",
                        now, keyEntity.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update LastUsedAt for key {KeyId}", keyEntity.Id);
                }
            });
        }

        return (keyEntity.Project, keyEntity);
    }

    public async Task EnsureDefaultProjectAndKeyAsync(string defaultKey)
    {
        if (string.IsNullOrWhiteSpace(defaultKey)) return;

        // 1. Ensure Default Project
        var defaultProject = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Code == "SMK_DEFAULT");
        if (defaultProject == null)
        {
            defaultProject = new Project
            {
                Id = Guid.NewGuid(),
                Code = "SMK_DEFAULT",
                Name = "Sammakorn Central Platform (Default)",
                Description = "Default project for internal services and legacy integrations",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Projects.Add(defaultProject);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Seeded default project: {Code}", defaultProject.Code);
        }

        // 2. Ensure Master Key exists
        var keyEntity = await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Key == defaultKey);
        if (keyEntity == null)
        {
            keyEntity = new ApiKey
            {
                Id = Guid.NewGuid(),
                ProjectId = defaultProject.Id,
                Key = defaultKey,
                Name = "Master API Key (Default)",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.ApiKeys.Add(keyEntity);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Seeded master API key for default project");
        }
    }

    public async Task<List<Project>> GetAllProjectsAsync()
    {
        return await _dbContext.Projects
            .Include(p => p.ApiKeys)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Project> CreateProjectAsync(string code, string name, string? description = null)
    {
        code = code.Trim().ToUpperInvariant();
        var existing = await _dbContext.Projects.FirstOrDefaultAsync(p => p.Code == code);
        if (existing != null)
        {
            throw new InvalidOperationException($"Project with code '{code}' already exists.");
        }

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name.Trim(),
            Description = description?.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Projects.Add(project);
        await _dbContext.SaveChangesAsync();
        return project;
    }

    public async Task<ApiKey> CreateApiKeyAsync(Guid projectId, string name, DateTime? expiresAt = null)
    {
        var project = await _dbContext.Projects.FindAsync(projectId);
        if (project == null)
        {
            throw new FileNotFoundException($"Project '{projectId}' not found.");
        }

        // Generate high-entropy API key: smk_live_<32 hex chars>
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        string generatedKey = $"smk_{project.Code.ToLowerInvariant()}_{Convert.ToHexString(bytes).ToLowerInvariant()}";

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Key = generatedKey,
            Name = name.Trim(),
            IsActive = true,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ApiKeys.Add(apiKey);
        await _dbContext.SaveChangesAsync();
        return apiKey;
    }

    public async Task<bool> RevokeApiKeyAsync(Guid apiKeyId)
    {
        var key = await _dbContext.ApiKeys.FindAsync(apiKeyId);
        if (key == null) return false;

        key.IsActive = !key.IsActive; // Toggle
        await _dbContext.SaveChangesAsync();
        return true;
    }
}
