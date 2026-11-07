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

public sealed class CreateProjectUseCase(
    IProjectRepository projectRepo,
    IUserProjectRoleRepository userRoleRepo,
    ICompanyRepository companyRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IApiKeyRepository? apiKeyRepo = null) : IUseCase<CreateProjectCommand, ProjectResultDto>
{
    public async Task<ProjectResultDto> ExecuteAsync(CreateProjectCommand command, CancellationToken ct = default)
    {
        var existing = await projectRepo.GetBySlugAsync(command.Slug, ct);
        if (existing != null)
        {
            throw new ConflictException($"Project with slug '{command.Slug}' already exists.");
        }

        var now = timeProvider.GetUtcNow();

        var defaultCompany = await companyRepo.GetFirstAsync(ct);
        if (defaultCompany == null)
        {
            defaultCompany = Company.Create(CompanyName.Create("Default Company"), now);
            await companyRepo.AddAsync(defaultCompany, ct);
        }

        var project = Project.Create(defaultCompany.Id, ProjectName.Create(command.Name), ProjectSlug.Create(command.Slug), now);
        await projectRepo.AddAsync(project, ct);

        var role = UserProjectRole.Create(new UserId(command.UserId), project.Id, RoleType.Admin, now);
        await userRoleRepo.AddAsync(role, ct);

        string? readPlainKey = null;
        string? writePlainKey = null;

        if (apiKeyRepo != null)
        {
            // Auto-provision Read API Key
            readPlainKey = $"smk_read_{command.Slug}_{Guid.NewGuid():N}";
            var readHash = new Sha256Hash(ApiKeyGenerator.ComputeHash(readPlainKey));
            var readKey = ApiKey.Issue(
                project.Id,
                ApiKeyName.Create($"{command.Name} Read"),
                "read-client",
                readHash,
                ExpirationPolicy.Never,
                now,
                ApiKeyScope.ReadOnly);
            await apiKeyRepo.AddAsync(readKey, ct);

            // Auto-provision Write/Delete API Key
            writePlainKey = $"smk_write_{command.Slug}_{Guid.NewGuid():N}";
            var writeHash = new Sha256Hash(ApiKeyGenerator.ComputeHash(writePlainKey));
            var writeKey = ApiKey.Issue(
                project.Id,
                ApiKeyName.Create($"{command.Name} Write"),
                "write-client",
                writeHash,
                ExpirationPolicy.Never,
                now,
                ApiKeyScope.ReadWrite);
            await apiKeyRepo.AddAsync(writeKey, ct);
        }

        // Single atomic transaction boundary
        await unitOfWork.CommitAsync(ct);

        return new ProjectResultDto(
            project.Id.Value,
            project.Name.Value,
            project.Slug.Value,
            project.IsActive,
            project.CreatedAt,
            readPlainKey,
            writePlainKey,
            ConcurrencyToken: (project.UpdatedAt ?? project.CreatedAt).ToUnixTimeMilliseconds().ToString());
    }
}
