using FluentValidation;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;

public record UpdateDataConnectionCommand(
    Guid Id,
    string Name,
    string Provider,
    string ConnectionString);

public sealed class UpdateDataConnectionUseCase(
    IDataConnectionRepository repository,
    IUnitOfWork unitOfWork,
    IDataProtectionService dataProtection,
    IValidator<UpdateDataConnectionCommand> validator,
    ISqlConnectionValidator sqlValidator,
    TimeProvider? timeProvider = null) : IUseCase<UpdateDataConnectionCommand, DataConnectionDto?>
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<DataConnectionDto?> ExecuteAsync(UpdateDataConnectionCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        var entity = await repository.GetByIdAsync(new DataConnectionId(command.Id), ct);
        if (entity == null) return null;

        EncryptedConnectionString encryptedCs;
        if (string.IsNullOrEmpty(command.ConnectionString))
        {
            var existingPlaintext = dataProtection.Decrypt(entity.EncryptedConnectionString.Value);
            await sqlValidator.ValidateConnectionTargetAsync(command.Provider, existingPlaintext, ct);
            encryptedCs = entity.EncryptedConnectionString;
        }
        else
        {
            await sqlValidator.ValidateConnectionTargetAsync(command.Provider, command.ConnectionString, ct);
            encryptedCs = EncryptedConnectionString.Create(dataProtection.Encrypt(command.ConnectionString));
        }

        var now = _timeProvider.GetUtcNow();
        entity.UpdateConnection(
            ConnectionName.Create(command.Name),
            Enumeration.FromDisplayName<DatabaseProvider>(command.Provider),
            encryptedCs,
            now);

        repository.Update(entity);
        await unitOfWork.CommitAsync(ct);

        return new DataConnectionDto(
            entity.Id.Value,
            entity.Name.Value,
            entity.Provider.Name,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
