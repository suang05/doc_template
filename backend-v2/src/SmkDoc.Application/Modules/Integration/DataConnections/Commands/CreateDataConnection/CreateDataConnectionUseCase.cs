using FluentValidation;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
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
    TimeProvider? timeProvider = null,
    ICurrentProjectContext? projectContext = null) : IUseCase<CreateDataConnectionCommand, DataConnectionDto>
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<DataConnectionDto> ExecuteAsync(CreateDataConnectionCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        var now = _timeProvider.GetUtcNow();
        var entity = DataConnection.Create(
            ConnectionName.Create(command.Name),
            Enumeration.FromDisplayName<DatabaseProvider>(command.Provider),
            EncryptedConnectionString.Create(dataProtection.Encrypt(command.ConnectionString)),
            now,
            projectContext?.ProjectId ?? default);

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
