namespace SmkDoc.Application.Common.Interfaces;

public interface ISqlExecutorService
{
    Task<string> ExecuteQueryAsJsonAsync(
        string provider,
        string connectionString,
        string sqlQuery,
        IDictionary<string, object>? parameters = null);

    Task<string> ExecuteQueryAsJsonAsync(
        string provider,
        string connectionString,
        string sqlQuery,
        IDictionary<string, object>? parameters,
        CancellationToken ct);
}
