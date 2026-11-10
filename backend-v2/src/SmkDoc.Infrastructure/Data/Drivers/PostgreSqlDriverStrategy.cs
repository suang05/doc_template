using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Streams;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;

namespace SmkDoc.Infrastructure.Data.Drivers;

/// <summary>
/// PostgreSQL database driver strategy utilizing Npgsql with transaction-level read-only guard.
/// Streams query results directly to JSON via DbDataReader and Utf8JsonWriter to eliminate LOH allocations.
/// </summary>
public sealed class PostgreSqlDriverStrategy : ISqlDriverStrategy
{
    public string ProviderName => "PostgreSQL";

    public async Task<string> ExecuteQueryAsJsonAsync(
        string connectionString,
        string sqlQuery,
        IDictionary<string, object>? parameters,
        int commandTimeoutSeconds,
        CancellationToken ct)
    {
        await using var dbConnection = new NpgsqlConnection(connectionString);
        await dbConnection.OpenAsync(ct);

        await using var transaction = await dbConnection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var readOnlyGuardCommand = new CommandDefinition(
            "SET TRANSACTION READ ONLY;",
            transaction: transaction,
            commandTimeout: commandTimeoutSeconds,
            cancellationToken: ct);
        await dbConnection.ExecuteAsync(readOnlyGuardCommand);

        var dapperParams = new DynamicParameters();
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                dapperParams.Add(param.Key, param.Value);
            }
        }

        var command = new CommandDefinition(
            sqlQuery,
            dapperParams,
            transaction: transaction,
            commandTimeout: commandTimeoutSeconds,
            cancellationToken: ct);

        await using var reader = await dbConnection.ExecuteReaderAsync(command);

        await using var spool = new HybridSpoolStream();
        await SerializeReaderToJsonStreamAsync(reader, spool, ct);

        await transaction.CommitAsync(ct);

        spool.Position = 0;
        using var streamReader = new StreamReader(spool, Encoding.UTF8);
        return await streamReader.ReadToEndAsync(ct);
    }

    internal static async Task SerializeReaderToJsonStreamAsync(DbDataReader reader, Stream destination, CancellationToken ct)
    {
        await using var jsonWriter = new Utf8JsonWriter(destination);
        jsonWriter.WriteStartArray();
        while (await reader.ReadAsync(ct))
        {
            jsonWriter.WriteStartObject();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                jsonWriter.WritePropertyName(reader.GetName(i));
                if (await reader.IsDBNullAsync(i, ct))
                {
                    jsonWriter.WriteNullValue();
                    continue;
                }

                WriteColumnValue(jsonWriter, reader, i);
            }
            jsonWriter.WriteEndObject();
        }
        jsonWriter.WriteEndArray();
        await jsonWriter.FlushAsync(ct);
    }

    internal static void WriteColumnValue(Utf8JsonWriter writer, DbDataReader reader, int ordinal)
    {
        var val = reader.GetValue(ordinal);
        switch (val)
        {
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case byte by:
                writer.WriteNumberValue(by);
                break;
            case sbyte sb:
                writer.WriteNumberValue(sb);
                break;
            case short s:
                writer.WriteNumberValue(s);
                break;
            case ushort us:
                writer.WriteNumberValue(us);
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case uint ui:
                writer.WriteNumberValue(ui);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case ulong ul:
                writer.WriteNumberValue(ul);
                break;
            case float f:
                writer.WriteNumberValue(f);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case decimal m:
                writer.WriteNumberValue(m);
                break;
            case string str:
                writer.WriteStringValue(str);
                break;
            case DateTime dt:
                writer.WriteStringValue(dt.ToString("O"));
                break;
            case DateTimeOffset dto:
                writer.WriteStringValue(dto.ToString("O"));
                break;
            case DateOnly date:
                writer.WriteStringValue(date.ToString("yyyy-MM-dd"));
                break;
            case TimeOnly time:
                writer.WriteStringValue(time.ToString("HH:mm:ss.fff"));
                break;
            case TimeSpan ts:
                writer.WriteStringValue(ts.ToString());
                break;
            case Guid g:
                writer.WriteStringValue(g);
                break;
            case byte[] bytes:
                writer.WriteBase64StringValue(bytes);
                break;
            default:
                writer.WriteStringValue(val.ToString());
                break;
        }
    }

    public async Task<DryRunExecutionResult> ExecuteDryRunAsync(
        string connectionString,
        string sqlQuery,
        IDictionary<string, object>? parameters,
        int maxRows,
        int commandTimeoutSeconds,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        await using var dbConnection = new NpgsqlConnection(connectionString);
        await dbConnection.OpenAsync(ct);

        await using var transaction = await dbConnection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var readOnlyGuardCommand = new CommandDefinition(
            "SET TRANSACTION READ ONLY;",
            transaction: transaction,
            commandTimeout: commandTimeoutSeconds,
            cancellationToken: ct);
        await dbConnection.ExecuteAsync(readOnlyGuardCommand);

        var dapperParams = new DynamicParameters();
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                dapperParams.Add(param.Key, param.Value);
            }
        }

        var command = new CommandDefinition(
            sqlQuery,
            dapperParams,
            transaction: transaction,
            commandTimeout: commandTimeoutSeconds,
            cancellationToken: ct);

        await using var reader = await dbConnection.ExecuteReaderAsync(command);

        var columnSchema = await reader.GetColumnSchemaAsync(ct);
        var columns = columnSchema.Select(c => new DatasetColumnSchemaDto(
            c.ColumnName,
            c.DataTypeName ?? c.DataType?.Name ?? "string",
            c.AllowDBNull ?? true)).ToList();

        var rows = new List<IDictionary<string, object?>>();
        int boundedLimit = Math.Clamp(maxRows, 1, 50);

        while (await reader.ReadAsync(ct) && rows.Count < boundedLimit)
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var val = reader.GetValue(i);
                row[reader.GetName(i)] = val is DBNull ? null : val;
            }
            rows.Add(row);
        }

        await transaction.CommitAsync(ct);
        stopwatch.Stop();

        return new DryRunExecutionResult(columns, rows, stopwatch.ElapsedMilliseconds);
    }
}

