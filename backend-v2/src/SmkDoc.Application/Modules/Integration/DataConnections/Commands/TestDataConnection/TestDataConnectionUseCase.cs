using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;

public record TestDataConnectionCommand(string Provider, string ConnectionString);

public sealed class TestDataConnectionUseCase(
    ISqlExecutorService sqlExecutor) : IUseCase<TestDataConnectionCommand, bool>
{
    public async Task<bool> ExecuteAsync(TestDataConnectionCommand command, CancellationToken ct = default)
    {
        try
        {
            var testQuery = "SELECT 1";
            await sqlExecutor.ExecuteQueryAsJsonAsync(command.Provider, command.ConnectionString, testQuery);
            return true;
        }
        catch (Exception)
        {
            // Connectivity test failure returns false rather than throwing
            return false;
        }
    }
}
