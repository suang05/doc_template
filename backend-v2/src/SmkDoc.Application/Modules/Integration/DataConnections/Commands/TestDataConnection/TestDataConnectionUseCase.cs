using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;

public record TestDataConnectionCommand(string Provider, string ConnectionString);

public sealed class TestDataConnectionUseCase(
    ISqlExecutorService sqlExecutor) : IUseCase<TestDataConnectionCommand, bool>
{
    public async Task<bool> ExecuteAsync(TestDataConnectionCommand command, CancellationToken ct = default)
    {
        try
        {
            const string testQuery = "SELECT 1";
            await sqlExecutor.ExecuteQueryAsJsonAsync(command.Provider, command.ConnectionString, testQuery, null, ct);
            return true;
        }
        catch (Exception ex) when (ex is ForbiddenException or DomainValidationException or OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Network/authentication connectivity test failure returns false rather than throwing
            return false;
        }
    }
}
