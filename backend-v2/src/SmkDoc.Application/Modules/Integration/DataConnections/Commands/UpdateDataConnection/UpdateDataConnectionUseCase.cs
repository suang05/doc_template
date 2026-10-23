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
    TimeProvider? timeProvider = null) : IUseCase<UpdateDataConnectionCommand, DataConnectionDto?>
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<DataConnectionDto?> ExecuteAsync(UpdateDataConnectionCommand command, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(new DataConnectionId(command.Id), ct);
        if (entity == null) return null;

        var encryptedCs = string.IsNullOrEmpty(command.ConnectionString)
            ? entity.EncryptedConnectionString
            : dataProtection.Encrypt(command.ConnectionString);

        var now = this.timeProvider.GetUtcNow();
        entity.UpdateConnection(
            ConnectionName.Create(command.Name),
            Enumeration.FromDisplayName<DatabaseProvider>(command.Provider),
            encryptedCs,
            now);

        repository.Update(entity);
        await unitOfWork.CommitAsync(ct);

        return new DataConnectionDto
        {
            Id = entity.Id.Value,
            Name = entity.Name.Value,
            Provider = entity.Provider.Name,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
