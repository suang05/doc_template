using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;

public record TestDataConnectionCommand(string Provider, string ConnectionString);

public sealed class TestDataConnectionUseCase(
    ISqlExecutorService sqlExecutor) : IUseCase<TestDataConnectionCommand, bool>
{
    private readonly ISqlExecutorService _sqlExecutor = sqlExecutor;

    public async Task<bool> ExecuteAsync(TestDataConnectionCommand command, CancellationToken ct = default)
    {
        try
        {
            var testQuery = "SELECT 1";
            await _sqlExecutor.ExecuteQueryAsJsonAsync(command.Provider, command.ConnectionString, testQuery);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
