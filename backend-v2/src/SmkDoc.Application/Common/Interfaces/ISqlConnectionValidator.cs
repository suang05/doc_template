namespace SmkDoc.Application.Common.Interfaces;

public interface ISqlConnectionValidator
{
    Task ValidateConnectionTargetAsync(string provider, string connectionString, CancellationToken ct = default);
    void ValidateReadOnlySql(string sqlQuery);
}
