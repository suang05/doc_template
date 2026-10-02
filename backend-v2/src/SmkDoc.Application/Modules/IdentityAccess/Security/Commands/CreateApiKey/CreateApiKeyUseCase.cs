using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;

public sealed class CreateApiKeyUseCase(
    IApiKeyRepository apiKeyRepo,
    IProjectRepository projectRepo,
    IUnitOfWork unitOfWork,
    IValidator<CreateApiKeyCommand> validator) : IUseCase<CreateApiKeyCommand, CreateApiKeyResult>
{
    private readonly IApiKeyRepository _apiKeyRepo = apiKeyRepo;
    private readonly IProjectRepository _projectRepo = projectRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IValidator<CreateApiKeyCommand> _validator = validator;

    public async Task<CreateApiKeyResult> ExecuteAsync(CreateApiKeyCommand command, CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new Domain.Exceptions.ValidationException(validationResult.ToDictionary());
        }

        Guid targetProjectId = command.ProjectId ?? Guid.Empty;
        if (targetProjectId == Guid.Empty)
        {
            var defaultProject = await _projectRepo.GetDefaultAsync(ct);
            if (defaultProject != null)
            {
                targetProjectId = defaultProject.Id;
            }
        }

        string rawSecret = $"smk_{command.CallerApp.ToLowerInvariant()}_{Guid.NewGuid():N}";
        string hash = ApiKeyHelper.ComputeHash(rawSecret);

        var key = new ApiKey(targetProjectId, command.Name, command.CallerApp, hash, null);

        await _apiKeyRepo.AddAsync(key, ct);
        await _unitOfWork.CommitAsync(ct);

        return new CreateApiKeyResult(key.Id, key.Name, key.CallerApp, rawSecret);
    }
}
