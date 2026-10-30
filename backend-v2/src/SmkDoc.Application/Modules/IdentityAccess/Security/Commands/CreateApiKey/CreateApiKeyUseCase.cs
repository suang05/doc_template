using FluentValidation;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;

public sealed class CreateApiKeyUseCase(
    IApiKeyRepository apiKeyRepo,
    IProjectRepository projectRepo,
    IUnitOfWork unitOfWork,
    IValidator<CreateApiKeyCommand> validator,
    ICurrentProjectContext projectContext,
    TimeProvider? timeProvider = null) : IUseCase<CreateApiKeyCommand, CreateApiKeyResultDto>
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<CreateApiKeyResultDto> ExecuteAsync(CreateApiKeyCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Active project context is required to create an API key.");
        }

        var pId = projectContext.ProjectId;
        _ = await projectRepo.GetByIdAsync(pId, ct)
            ?? throw new NotFoundException($"Project '{pId.Value}' not found.");

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
