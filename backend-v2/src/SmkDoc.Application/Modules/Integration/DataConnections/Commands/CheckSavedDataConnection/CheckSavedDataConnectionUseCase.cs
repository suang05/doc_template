using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.CheckSavedDataConnection;

public sealed class CheckSavedDataConnectionUseCase(
    IDataConnectionRepository repository,
    IDataProtectionService dataProtection,
    ISqlExecutorService sqlExecutor) : IUseCase<CheckSavedDataConnectionCommand, TestDataConnectionResultDto>
{
    public async Task<TestDataConnectionResultDto> ExecuteAsync(CheckSavedDataConnectionCommand command, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(new DataConnectionId(command.Id), ct)
            ?? throw new NotFoundException("DataConnection", command.Id);

        string connectionString = dataProtection.Decrypt(entity.EncryptedConnectionString.Value);
        return await DataConnectionConnectivityProbe.ExecuteAsync(
            sqlExecutor,
            entity.Provider.Name,
            connectionString,
            ct);
    }
}
