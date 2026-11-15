using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;

public sealed class UpdateDataConnectionUseCase(
    IDataConnectionRepository repository,
    IUnitOfWork unitOfWork,
    IDataProtectionService dataProtection,
    IConnectionHostValidator connectionValidator,
    TimeProvider timeProvider) : IUseCase<UpdateDataConnectionCommand, DataConnectionResultDto>
{
    public async Task<DataConnectionResultDto> ExecuteAsync(UpdateDataConnectionCommand command, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(new DataConnectionId(command.Id), ct)
            ?? throw new NotFoundException("DataConnection", command.Id);

        if (entity.Version != command.ExpectedVersion)
        {
            throw PreconditionFailedException.VersionMismatch();
        }

        EncryptedConnectionString encryptedCs;
        if (string.IsNullOrEmpty(command.ConnectionString))
        {
            var existingPlaintext = dataProtection.Decrypt(entity.EncryptedConnectionString.Value);
            await connectionValidator.ValidateConnectionTargetAsync(command.Provider, existingPlaintext, entity.ProjectId, ct);
            encryptedCs = entity.EncryptedConnectionString;
        }
        else
        {
            await connectionValidator.ValidateConnectionTargetAsync(command.Provider, command.ConnectionString, entity.ProjectId, ct);
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

        return new DataConnectionResultDto(
            entity.Id.Value,
            entity.Name.Value,
            entity.Provider.Name,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.Version);
    }
}
