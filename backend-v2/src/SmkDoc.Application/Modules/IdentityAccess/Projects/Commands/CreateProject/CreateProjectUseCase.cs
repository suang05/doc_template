using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;

/// <summary>
/// Provisions a new tenant project aggregate, binds the creating user as ProjectAdmin,
/// and auto-provisions deterministic ReadOnly and ReadWrite sub-keys under quota guardrails.
/// </summary>
public sealed class CreateProjectUseCase(
    IProjectRepository projectRepo,
    IUserProjectRoleRepository userRoleRepo,
    IApiKeyRepository apiKeyRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<CreateProjectCommand, ProjectResultDto>
{
    public async Task<ProjectResultDto> ExecuteAsync(CreateProjectCommand command, CancellationToken ct = default)
    {
        await EnsureUniqueSlugAsync(command.Slug, ct);

        var now = timeProvider.GetUtcNow();
        var project = await ProvisionProjectAggregateAsync(command, now, ct);
        var (readKey, writeKey) = await ProvisionInitialApiKeysAsync(project.Id, command.Name, command.Slug, now, ct);
        await unitOfWork.CommitAsync(ct);

        return ToResultDto(project, readKey, writeKey);
    }

    private async Task<Project> ProvisionProjectAggregateAsync(CreateProjectCommand command, DateTimeOffset now, CancellationToken ct)
    {
        var companyId = await projectRepo.GetOrCreateDefaultCompanyIdAsync(now, ct);
        var project = Project.Create(companyId, ProjectName.Create(command.Name), ProjectSlug.Create(command.Slug), now);
        await projectRepo.AddAsync(project, ct);

        var role = UserProjectRole.Create(new UserId(command.UserId), project.Id, RoleType.Admin, now);
        await userRoleRepo.AddAsync(role, ct);
        return project;
    }

    private static ProjectResultDto ToResultDto(Project project, string readKey, string writeKey) =>
        new(project.Id.Value,
            project.Name.Value,
            project.Slug.Value,
            project.IsActive,
            project.CreatedAt,
            readKey,
            writeKey,
            ConcurrencyToken: project.GetConcurrencyToken());

    private async Task EnsureUniqueSlugAsync(string slug, CancellationToken ct)
    {
        var existing = await projectRepo.GetBySlugAsync(slug, ct);
        if (existing != null)
        {
            throw new ConflictException($"Project with slug '{slug}' already exists.");
        }
    }

    private async Task<(string ReadKey, string WriteKey)> ProvisionInitialApiKeysAsync(
        ProjectId projectId,
        string name,
        string slug,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var read = await ProvisionApiKeyAsync(projectId, $"{name} Read", "read-client", $"smk_read_{slug}", ApiKeyScope.ReadOnly, now, ct);
        var write = await ProvisionApiKeyAsync(projectId, $"{name} Write", "write-client", $"smk_write_{slug}", ApiKeyScope.ReadWrite, now, ct);
        return (read, write);
    }

    private async Task<string> ProvisionApiKeyAsync(
        ProjectId projectId,
        string name,
        string callerApp,
        string prefix,
        ApiKeyScope scope,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var plain = $"{prefix}_{Guid.NewGuid():N}";
        var hash = new Sha256Hash(ApiKeyGenerator.ComputeHash(plain));
        var key = ApiKey.Issue(projectId, ApiKeyName.Create(name), callerApp, hash, ExpirationPolicy.Never, now, scope);
        await apiKeyRepo.AddAsync(key, ct);
        return plain;
    }
}
