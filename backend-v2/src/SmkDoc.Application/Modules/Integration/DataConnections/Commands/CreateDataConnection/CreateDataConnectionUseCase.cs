using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;

public sealed class CreateDataConnectionUseCase(
    IDataConnectionRepository repository,
    IUnitOfWork unitOfWork,
    IDataProtectionService dataProtection,
    ISqlConnectionValidator sqlValidator,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IUseCase<CreateDataConnectionCommand, DataConnectionResultDto>
{
    public async Task<DataConnectionResultDto> ExecuteAsync(CreateDataConnectionCommand command, CancellationToken ct = default)
    {
        if (!projectContext.IsResolved)
        {
            throw new ForbiddenException("Creating a data connection requires an active project context.");
        }

        await sqlValidator.ValidateConnectionTargetAsync(command.Provider, command.ConnectionString, ct);

        var now = timeProvider.GetUtcNow();
        var entity = DataConnection.Create(
            ConnectionName.Create(command.Name),
            Enumeration.FromDisplayName<DatabaseProvider>(command.Provider),
            EncryptedConnectionString.Create(dataProtection.Encrypt(command.ConnectionString)),
            now,
            projectContext.ProjectId);

        await repository.AddAsync(entity, ct);
        await unitOfWork.CommitAsync(ct);

        return new DataConnectionResultDto(
            entity.Id.Value,
            entity.Name.Value,
            entity.Provider.Name,
            entity.CreatedAt,
            entity.UpdatedAt,
            (entity.UpdatedAt ?? entity.CreatedAt).ToUnixTimeMilliseconds().ToString());
    }
}
