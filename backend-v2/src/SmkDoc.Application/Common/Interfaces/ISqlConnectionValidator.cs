namespace SmkDoc.Application.Common.Interfaces;

public interface ISqlConnectionValidator
{
    Task ValidateConnectionTargetAsync(string provider, string connectionString, CancellationToken ct);
    void ValidateReadOnlySql(string sqlQuery);
}
