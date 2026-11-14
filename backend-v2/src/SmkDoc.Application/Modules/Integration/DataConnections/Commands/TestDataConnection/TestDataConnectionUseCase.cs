using System.Data.Common;
using System.Net.Sockets;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;

public sealed class TestDataConnectionUseCase(
    ISqlExecutorService sqlExecutor) : IUseCase<TestDataConnectionCommand, TestDataConnectionResultDto>
{
    public async Task<TestDataConnectionResultDto> ExecuteAsync(TestDataConnectionCommand command, CancellationToken ct)
    {
        try
        {
            const string testQuery = "SELECT 1";
            await sqlExecutor.ExecuteQueryAsJsonAsync(command.Provider, command.ConnectionString, testQuery, null, ct);
            return new TestDataConnectionResultDto(true, "Connection successful.");
        }
        catch (DbException ex)
        {
            return new TestDataConnectionResultDto(false, "Connection failed.", ex.Message);
        }
        catch (SocketException ex)
        {
            return new TestDataConnectionResultDto(false, "Connection failed.", ex.Message);
        }
        catch (TimeoutException ex)
        {
            return new TestDataConnectionResultDto(false, "Connection failed.", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return new TestDataConnectionResultDto(false, "Connection failed.", ex.Message);
        }
        catch (ArgumentException ex)
        {
            return new TestDataConnectionResultDto(false, "Connection failed.", ex.Message);
        }
        catch (DatasetExecutionException ex)
        {
            return new TestDataConnectionResultDto(false, "Connection failed.", ex.Message);
        }
    }
}
