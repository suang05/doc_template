using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ValidateApiKey;

public sealed class ValidateApiKeyUseCase(
    IApiKeyRepository apiKeyRepo,
    IProjectRepository projectRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IApiKeyVerifier? apiKeyVerifier = null,
    IProjectStatusReader? projectStatusReader = null,
    ISecurityAuditService? securityAuditService = null) : IUseCase<ValidateApiKeyQuery, ValidatedApiKeyResultDto?>
{
    public async Task<ValidatedApiKeyResultDto?> ExecuteAsync(ValidateApiKeyQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.PlainTextKey)) return null;

        var now = timeProvider.GetUtcNow();

        if (apiKeyVerifier is not null)
        {
            var verified = await apiKeyVerifier.VerifyAsync(query.PlainTextKey, ct);
            if (verified is null)
            {
                return null;
            }

            var isProjectActive = projectStatusReader is not null
                ? (await projectStatusReader.FindProjectStatusAsync(verified.ProjectId, ct))?.IsActive ?? false
                : (await projectRepo.GetByIdAsync(verified.ProjectId, ct))?.IsActive ?? false;

            if (!isProjectActive)
            {
                throw new ProjectDeactivatedException(verified.ProjectId.Value);
            }

            await apiKeyVerifier.TouchUsageAsync(verified.KeyId, now, ct);
            if (verified.IsLegacySha256)
            {
                securityAuditService?.LogLegacyApiKeyUsed(verified.KeyId.Value, verified.ProjectId.Value);
            }

            return new ValidatedApiKeyResultDto(verified.KeyId.Value, verified.CallerApp, verified.ProjectId.Value, verified.Scope.Name);
        }

        var hash = new Sha256Hash(ApiKeyGenerator.ComputeHash(query.PlainTextKey));
        var key = await apiKeyRepo.GetByKeyHashAsync(hash, ct);

        if (key == null || !key.IsUsableAt(now)) return null;

        return await ActivateAndRecordUsageAsync(key, now, ct);
    }

    private async Task<ValidatedApiKeyResultDto> ActivateAndRecordUsageAsync(
        Domain.Entities.ApiKey key,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var isProjectActive = projectStatusReader is not null
            ? (await projectStatusReader.FindProjectStatusAsync(key.ProjectId, ct))?.IsActive ?? false
            : (await projectRepo.GetByIdAsync(key.ProjectId, ct))?.IsActive ?? false;

        if (!isProjectActive)
        {
            throw new ProjectDeactivatedException(key.ProjectId.Value);
        }

        key.RecordUsage(now);
        apiKeyRepo.Update(key);
        await unitOfWork.CommitAsync(ct);

        return new ValidatedApiKeyResultDto(key.Id.Value, key.CallerApp, key.ProjectId.Value, key.Scope.Name);
    }
}
