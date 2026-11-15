using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Infrastructure.Data;

/// <summary>
/// Orchestrates external SQL dataset execution by enforcing SSRF/egress pinning and read-only AST guards
/// before dispatching to polymorphic database driver strategies.
/// </summary>
public sealed class SqlExecutorService(
    IConnectionHostValidator connectionValidator,
    ISqlDriverFactory driverFactory,
    ICurrentProjectContext? projectContext = null) : ISqlExecutorService
{
    private const int DefaultCommandTimeoutSeconds = 15;

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

        var pinnedTarget = await connectionValidator.ValidateConnectionTargetAsync(
            provider,
            connectionString,
            projectContext?.TryGetProjectId(),
            ct);
        connectionValidator.ValidateReadOnlySql(sqlQuery);

        var strategy = driverFactory.GetStrategy(provider);
        return await strategy.ExecuteQueryWithPinnedTargetAsync(
            connectionString,
            sqlQuery,
            parameters,
            DefaultCommandTimeoutSeconds,
            pinnedTarget,
            ct);
    }
}
