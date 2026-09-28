using System.Security.Cryptography;
using System.Text;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.IdentityAccess.Security;

public sealed class ApiKeyUseCase(
    IRepository<ApiKey> apiKeyRepo,
    IUnitOfWork unitOfWork,
    IRepository<Project>? projectRepo = null)
{
    private readonly IRepository<ApiKey> _apiKeyRepo = apiKeyRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IRepository<Project>? _projectRepo = projectRepo;

    public async Task<ValidatedApiKeyDto?> ValidateKeyAsync(string plainTextKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(plainTextKey)) return null;

        string hash = ComputeHash(plainTextKey);
        var key = await _apiKeyRepo.FirstOrDefaultAsync(k => k.KeyHash == hash && k.IsActive, ct);
        if (key != null)
        {
            key.RecordUsage();
            _apiKeyRepo.Update(key);
            await _unitOfWork.CommitAsync(ct);
            return new ValidatedApiKeyDto(key.Id, key.CallerApp, key.ProjectId);
        }
        return null;
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

        var key = new ApiKey(targetProjectId, name, callerApp, hash, null);

        await _apiKeyRepo.AddAsync(key, ct);
        await _unitOfWork.CommitAsync(ct);

        return new CreateApiKeyResult(key.Id, key.Name, key.CallerApp, rawSecret);
    }

    public async Task RevokeKeyAsync(Guid id, CancellationToken ct = default)
    {
        var key = await _apiKeyRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"API Key '{id}' not found.");

        key.Revoke();
        _apiKeyRepo.Update(key);
        await _unitOfWork.CommitAsync(ct);
    }

    public static string ComputeHash(string input)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
