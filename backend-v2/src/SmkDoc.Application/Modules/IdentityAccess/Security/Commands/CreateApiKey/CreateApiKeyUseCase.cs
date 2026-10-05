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
    IValidator<CreateApiKeyCommand> validator,
    IExecutionContext? executionContext = null) : IUseCase<CreateApiKeyCommand, CreateApiKeyResultDto>
{
    private readonly IApiKeyRepository _apiKeyRepo = apiKeyRepo;
    private readonly IProjectRepository _projectRepo = projectRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IValidator<CreateApiKeyCommand> _validator = validator;
    private readonly IExecutionContext? _executionContext = executionContext;

    public async Task<CreateApiKeyResultDto> ExecuteAsync(CreateApiKeyCommand command, CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        Guid targetProjectId = command.ProjectId 
            ?? (_executionContext?.ProjectId.HasValue == true ? _executionContext.ProjectId.Value : Guid.Empty);

        if (targetProjectId == Guid.Empty)
        {
            throw new ValidationException("ProjectId", "ProjectId is required to create an API key.");
        }

        var project = await _projectRepo.GetByIdAsync(targetProjectId, ct)
            ?? throw new NotFoundException($"Project '{targetProjectId}' not found.");

        string rawSecret = $"smk_{command.CallerApp.ToLowerInvariant()}_{Guid.NewGuid():N}";
        string hash = ApiKeyHelper.ComputeHash(rawSecret);

        var key = new ApiKey(targetProjectId, command.Name, command.CallerApp, hash, null);

        await _apiKeyRepo.AddAsync(key, ct);
        await _unitOfWork.CommitAsync(ct);

        return new CreateApiKeyResultDto(key.Id, key.Name, key.CallerApp, rawSecret);
    }
}
