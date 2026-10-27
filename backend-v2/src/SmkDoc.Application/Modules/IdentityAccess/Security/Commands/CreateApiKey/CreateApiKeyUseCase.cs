using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;

public sealed class CreateApiKeyUseCase(
    IApiKeyRepository apiKeyRepo,
    IProjectRepository projectRepo,
    IUnitOfWork unitOfWork,
    IValidator<CreateApiKeyCommand> validator,
    TimeProvider? timeProvider = null,
    ICurrentProjectContext? projectContext = null) : IUseCase<CreateApiKeyCommand, CreateApiKeyResultDto>
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<CreateApiKeyResultDto> ExecuteAsync(CreateApiKeyCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        Guid targetProjectId = command.ProjectId 
            ?? (projectContext?.IsResolved == true ? projectContext.ProjectId.Value : Guid.Empty);

        if (targetProjectId == Guid.Empty)
        {
            throw new ValidationException("ProjectId", "ProjectId is required to create an API key.");
        }

        var pId = new ProjectId(targetProjectId);
        var project = await projectRepo.GetByIdAsync(pId, ct)
            ?? throw new NotFoundException($"Project '{targetProjectId}' not found.");

        string rawSecret = $"smk_{command.CallerApp.ToLowerInvariant()}_{Guid.NewGuid():N}";
        var hash = new Sha256Hash(ApiKeyHelper.ComputeHash(rawSecret));

        var key = ApiKey.Issue(
            pId,
            ApiKeyName.Create(command.Name),
            command.CallerApp,
            hash,
            ExpirationPolicy.Never,
            now: this.timeProvider.GetUtcNow());

        await apiKeyRepo.AddAsync(key, ct);
        await unitOfWork.CommitAsync(ct);

        return new CreateApiKeyResultDto(key.Id.Value, key.Name.Value, key.CallerApp, rawSecret);
    }
}
