using FluentValidation;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;

public record CreateDataConnectionCommand(
    string Name,
    string Provider,
    string ConnectionString);

public sealed class CreateDataConnectionUseCase(
    IDataConnectionRepository repository,
    IUnitOfWork unitOfWork,
    IDataProtectionService dataProtection,
    IValidator<CreateDataConnectionCommand> validator,
    ISqlConnectionValidator sqlValidator,
    ICurrentProjectContext projectContext,
    TimeProvider? timeProvider = null) : IUseCase<CreateDataConnectionCommand, DataConnectionDto>
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<DataConnectionDto> ExecuteAsync(CreateDataConnectionCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new SmkDoc.Application.Common.Exceptions.ValidationException(validationResult.ToDictionary());
        }

        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Creating a data connection requires an active project context.");
        }

        await sqlValidator.ValidateConnectionTargetAsync(command.Provider, command.ConnectionString, ct);

        var now = _timeProvider.GetUtcNow();
        var entity = DataConnection.Create(
            ConnectionName.Create(command.Name),
            Enumeration.FromDisplayName<DatabaseProvider>(command.Provider),
            EncryptedConnectionString.Create(dataProtection.Encrypt(command.ConnectionString)),
            now,
            projectContext.ProjectId);

        await repository.AddAsync(entity, ct);
        await unitOfWork.CommitAsync(ct);

        return new DataConnectionDto(
            entity.Id.Value,
            entity.Name.Value,
            entity.Provider.Name,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
