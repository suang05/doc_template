using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Infrastructure.Data;

/// <summary>
/// Orchestrates external SQL dataset execution by enforcing security validation (SSRF and read-only AST)
/// and dispatching to polymorphic database driver strategies.
/// </summary>
public sealed class SqlExecutorService(
    ISqlConnectionValidator sqlValidator,
    ISqlDriverFactory driverFactory) : ISqlExecutorService
{
    private const int DefaultCommandTimeoutSeconds = 15;

    public Task<string> ExecuteQueryAsJsonAsync(
        string provider,
        string connectionString,
        string sqlQuery,
        IDictionary<string, object>? parameters = null) =>
        ExecuteQueryAsJsonAsync(provider, connectionString, sqlQuery, parameters, default);

    public async Task<string> ExecuteQueryAsJsonAsync(
        string provider,
        string connectionString,
        string sqlQuery,
        IDictionary<string, object>? parameters,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new DomainValidationException("Data connection provider is required.");
        }

        await sqlValidator.ValidateConnectionTargetAsync(provider, connectionString, ct);
        sqlValidator.ValidateReadOnlySql(sqlQuery);

        var strategy = driverFactory.GetStrategy(provider);
        return await strategy.ExecuteQueryAsJsonAsync(
            connectionString,
            sqlQuery,
            parameters,
            DefaultCommandTimeoutSeconds,
            ct);
    }
}
