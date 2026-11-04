using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;

public sealed class TestDataConnectionUseCase(
    ISqlExecutorService sqlExecutor) : IUseCase<TestDataConnectionCommand, TestDataConnectionResultDto>
{
    public async Task<TestDataConnectionResultDto> ExecuteAsync(TestDataConnectionCommand command, CancellationToken ct = default)
    {
        try
        {
            const string testQuery = "SELECT 1";
            await sqlExecutor.ExecuteQueryAsJsonAsync(command.Provider, command.ConnectionString, testQuery, null, ct);
            return new TestDataConnectionResultDto(true, "Connection successful.");
        }
        catch (Exception ex) when (ex is ForbiddenException or DomainValidationException or OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Network/authentication connectivity test failure returns false rather than throwing
            return new TestDataConnectionResultDto(false, "Connection failed.", ex.Message);
        }
    }
}
