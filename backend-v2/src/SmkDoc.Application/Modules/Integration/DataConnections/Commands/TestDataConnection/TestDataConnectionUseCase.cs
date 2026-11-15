using SmkDoc.Application.Modules.Integration.DataConnections.Commands.CheckSavedDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;

public sealed class TestDataConnectionUseCase(
    ISqlExecutorService sqlExecutor) : IUseCase<TestDataConnectionCommand, TestDataConnectionResultDto>
{
    public Task<TestDataConnectionResultDto> ExecuteAsync(TestDataConnectionCommand command, CancellationToken ct) =>
        DataConnectionConnectivityProbe.ExecuteAsync(
            sqlExecutor,
            command.Provider,
            command.ConnectionString,
            ct);
}
