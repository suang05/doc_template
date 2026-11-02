using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;

public sealed class UpdateDataConnectionUseCase(
    IDataConnectionRepository repository,
    IUnitOfWork unitOfWork,
    IDataProtectionService dataProtection,
    ISqlConnectionValidator sqlValidator,
    TimeProvider timeProvider) : IUseCase<UpdateDataConnectionCommand, DataConnectionDto?>
{
    public async Task<DataConnectionDto?> ExecuteAsync(UpdateDataConnectionCommand command, CancellationToken ct = default)
    {
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

        var now = timeProvider.GetUtcNow();
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
