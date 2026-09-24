using System.Security.Cryptography;
using System.Text;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.UseCases.Security;

public record ApiKeyDto(
    Guid Id,
    string Name,
    string CallerApp,
    bool IsActive,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset CreatedAt
);

public record CreateApiKeyResult(
    Guid Id,
    string Name,
    string CallerApp,
    string PlainTextKey
);

public class ApiKeyUseCase
{
    private readonly IRepository<ApiKey> _apiKeyRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Project>? _projectRepo;

    public ApiKeyUseCase(
        IRepository<ApiKey> apiKeyRepo,
        IUnitOfWork unitOfWork,
        IRepository<Project>? projectRepo = null)
    {
        _apiKeyRepo = apiKeyRepo;
        _unitOfWork = unitOfWork;
        _projectRepo = projectRepo;
    }

    public async Task<ApiKey?> ValidateKeyAsync(string plainTextKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(plainTextKey)) return null;

        string hash = ComputeHash(plainTextKey);
        var key = await _apiKeyRepo.FirstOrDefaultAsync(k => k.KeyHash == hash && k.IsActive, ct);
        if (key != null)
        {
            key.LastUsedAt = DateTimeOffset.UtcNow;
            _apiKeyRepo.Update(key);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        return key;
    }

    public async Task<List<ApiKeyDto>> ListKeysAsync(CancellationToken ct = default)
    {
        var keys = await _apiKeyRepo.ListAsync(null, ct);
        return keys
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new ApiKeyDto(k.Id, k.Name, k.CallerApp, k.IsActive, k.LastUsedAt, k.CreatedAt))
            .ToList();
    }

    public async Task<CreateApiKeyResult> CreateKeyAsync(string name, string callerApp, Guid? projectId = null, CancellationToken ct = default)
    {
        Guid targetProjectId = projectId ?? Guid.Empty;
        if (targetProjectId == Guid.Empty && _projectRepo != null)
        {
            var defaultProject = await _projectRepo.FirstOrDefaultAsync(p => p.IsActive, ct);
            if (defaultProject != null)
            {
                targetProjectId = defaultProject.Id;
            }
        }

        string rawSecret = $"smk_{callerApp.ToLowerInvariant()}_{Guid.NewGuid():N}";
        string hash = ComputeHash(rawSecret);

        var key = new ApiKey
        {
            Id = Guid.NewGuid(),
            ProjectId = targetProjectId,
            Name = name,
            CallerApp = callerApp,
            KeyHash = hash,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _apiKeyRepo.AddAsync(key, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new CreateApiKeyResult(key.Id, key.Name, key.CallerApp, rawSecret);
    }

    public async Task RevokeKeyAsync(Guid id, CancellationToken ct = default)
    {
        var key = await _apiKeyRepo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"API Key '{id}' not found.");

        key.IsActive = false;
        _apiKeyRepo.Update(key);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public static string ComputeHash(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
