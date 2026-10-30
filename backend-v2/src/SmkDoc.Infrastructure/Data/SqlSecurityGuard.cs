using System.Data.Common;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Infrastructure.Data;

/// <summary>
/// Enforces network-level SSRF protection (blocking loopback, unspecified, link-local cloud metadata,
/// multi-host strings, and SmkDoc's own database) and lexical read-only SELECT/WITH SQL rules.
/// </summary>
public sealed partial class SqlSecurityGuard(
    IConfiguration? configuration = null,
    Func<string, CancellationToken, Task<IPAddress[]>>? dnsResolver = null) : ISqlConnectionValidator
{
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _dnsResolver =
        dnsResolver ?? ((host, token) => Dns.GetHostAddressesAsync(host, token));

    private static readonly HashSet<string> ForbiddenSqlTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "TRUNCATE", "CREATE",
        "GRANT", "REVOKE", "COPY", "CALL", "DO", "EXECUTE", "SET", "VACUUM",
        "COMMENT", "LOCK", "DISCARD", "LISTEN", "NOTIFY", "PREPARE", "DEALLOCATE",
        "PG_READ_FILE", "PG_READ_BINARY_FILE", "PG_LS_DIR", "PG_STAT_FILE",
        "PG_SLEEP", "PG_TERMINATE_BACKEND", "PG_CANCEL_BACKEND", "PG_RELOAD_CONF",
        "LO_IMPORT", "LO_EXPORT", "DBLINK", "DBLINK_EXEC"
    };

    [GeneratedRegex(@"[a-zA-Z_][a-zA-Z0-9_$]*", RegexOptions.Compiled)]
    private static partial Regex SqlIdentifierRegex();

    public async Task ValidateConnectionTargetAsync(string provider, string connectionString, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new DomainValidationException("Database provider is required.");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new DomainValidationException("Database connection string cannot be empty.");
        }

        var (host, port, database) = ParseConnectionEndpoint(provider, connectionString);

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new DomainValidationException("Connection string must specify a valid Host.");
        }

        if (string.IsNullOrWhiteSpace(database))
        {
            throw new DomainValidationException("Connection string must specify a target Database.");
        }

        if (host.Contains(','))
        {
            throw new ForbiddenException("Multiple hosts in external data connection strings are prohibited.");
        }

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Connecting to loopback host 'localhost' is prohibited.");
        }

        IPAddress[] targetAddresses = await ResolveAddressesAsync(host, ct);
        if (targetAddresses.Length == 0)
        {
            throw new DomainValidationException($"Unable to resolve database host '{host}'.");
        }

        foreach (var rawIp in targetAddresses)
        {
            ValidateIpAddress(rawIp, host);
        }

        await EnforceNotSelfDatabaseAsync(host, port, database, targetAddresses, ct);
    }

    public void ValidateReadOnlySql(string sqlQuery)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
        {
            throw new DomainValidationException("SQL query cannot be empty.");
        }

        string sanitized = SanitizeCommentsAndLiterals(sqlQuery).Trim();
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new DomainValidationException("SQL query contains no executable SELECT statement.");
        }

        if (sanitized.EndsWith(';'))
        {
            sanitized = sanitized[..^1].TrimEnd();
        }

        if (sanitized.Contains(';'))
        {
            throw new DomainValidationException("Stacked SQL statements (multiple semicolons) are prohibited.");
        }

        var matches = SqlIdentifierRegex().Matches(sanitized);
        if (matches.Count == 0)
        {
            throw new DomainValidationException("SQL query contains no valid statement.");
        }

        string firstKeyword = matches[0].Value;
        if (!string.Equals(firstKeyword, "SELECT", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(firstKeyword, "WITH", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainValidationException(
                $"Only read-only SELECT or WITH queries are permitted. Found leading keyword '{firstKeyword}'.");
        }

        bool hasSelectKeyword = false;
        foreach (Match match in matches)
        {
            string token = match.Value;
            if (string.Equals(token, "SELECT", StringComparison.OrdinalIgnoreCase))
            {
                hasSelectKeyword = true;
            }

            if (ForbiddenSqlTokens.Contains(token))
            {
                throw new DomainValidationException(
                    $"Forbidden SQL keyword or administrative function '{token}' is prohibited in dataset queries.");
            }
        }

        if (!hasSelectKeyword)
        {
            throw new DomainValidationException("CTE query must contain a SELECT clause.");
        }
    }

    private static (string Host, int Port, string Database) ParseConnectionEndpoint(string provider, string connectionString)
    {
        if (string.Equals(provider.Trim(), "postgresql", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString);
                int port = builder.Port > 0 ? builder.Port : 5432;
                return ((builder.Host ?? string.Empty).Trim(), port, (builder.Database ?? string.Empty).Trim());
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                throw new DomainValidationException($"Invalid PostgreSQL connection string format: {ex.Message}");
            }
        }

        try
        {
            var generic = new DbConnectionStringBuilder { ConnectionString = connectionString };
            string host = GetFirstKey(generic, "Host", "Server", "Data Source");
            string db = GetFirstKey(generic, "Database", "Initial Catalog");
            string portStr = GetFirstKey(generic, "Port");
            int port = int.TryParse(portStr, out int p) && p > 0 ? p : 0;

            if (host.Contains(':') && !host.StartsWith('['))
            {
                var parts = host.Split(':', 2);
                host = parts[0];
                if (port == 0 && int.TryParse(parts[1], out int parsedPort))
                {
                    port = parsedPort;
                }
            }
            else if (host.Contains(',') && int.TryParse(host.Split(',', 2)[1], out int sqlServerPort))
            {
                host = host.Split(',', 2)[0];
                if (port == 0)
                {
                    port = sqlServerPort;
                }
            }

            return (host.Trim(), port, db.Trim());
        }
        catch (ArgumentException ex)
        {
            throw new DomainValidationException($"Invalid connection string format: {ex.Message}");
        }
    }

    private static string GetFirstKey(DbConnectionStringBuilder builder, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (builder.TryGetValue(key, out object? val) && val is not null)
            {
                var str = val.ToString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    return str;
                }
            }
        }

        return string.Empty;
    }

    private async Task<IPAddress[]> ResolveAddressesAsync(string host, CancellationToken ct)
    {
        var unbracketed = host.Trim('[', ']');
        if (IPAddress.TryParse(unbracketed, out var literalIp))
        {
            return [literalIp];
        }

        try
        {
            return await _dnsResolver(unbracketed, ct);
        }
        catch (SocketException ex)
        {
            throw new DomainValidationException($"Failed to resolve database host '{host}': {ex.Message}");
        }
    }

    private static void ValidateIpAddress(IPAddress ip, string originalHost)
    {
        var normalized = ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;

        if (IPAddress.IsLoopback(normalized))
        {
            throw new ForbiddenException($"Connecting to loopback address '{normalized}' (host '{originalHost}') is prohibited.");
        }

        if (normalized.Equals(IPAddress.Any) || normalized.Equals(IPAddress.IPv6Any))
        {
            throw new ForbiddenException($"Connecting to unspecified address '{normalized}' is prohibited.");
        }

        if (normalized.AddressFamily == AddressFamily.InterNetwork)
        {
            byte[] bytes = normalized.GetAddressBytes();
            // Block 0.0.0.0/8 ("this" network) and 169.254.0.0/16 (Link-Local / Cloud Instance Metadata)
            if (bytes[0] == 0 || (bytes[0] == 169 && bytes[1] == 254))
            {
                throw new ForbiddenException(
                    $"Connecting to link-local or metadata address '{normalized}' (host '{originalHost}') is prohibited.");
            }
        }
        else if (normalized.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (normalized.IsIPv6LinkLocal)
            {
                throw new ForbiddenException(
                    $"Connecting to IPv6 link-local address '{normalized}' (host '{originalHost}') is prohibited.");
            }
        }
    }

    private async Task EnforceNotSelfDatabaseAsync(
        string targetHost,
        int targetPort,
        string targetDatabase,
        IPAddress[] targetIps,
        CancellationToken ct)
    {
        string? internalConnStr = configuration?.GetConnectionString("DefaultConnection")
            ?? configuration?["DATABASE_URL"];

        if (string.IsNullOrWhiteSpace(internalConnStr))
        {
            return;
        }

        NpgsqlConnectionStringBuilder internalBuilder;
        try
        {
            internalBuilder = new NpgsqlConnectionStringBuilder(internalConnStr);
        }
        catch
        {
            return;
        }

        string internalHost = (internalBuilder.Host ?? string.Empty).Trim();
        int internalPort = internalBuilder.Port > 0 ? internalBuilder.Port : 5432;
        string internalDb = (internalBuilder.Database ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(internalHost) || string.IsNullOrWhiteSpace(internalDb))
        {
            return;
        }

        int normalizedTargetPort = targetPort > 0 ? targetPort : 5432;
        if (normalizedTargetPort != internalPort ||
            !string.Equals(targetDatabase, internalDb, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(targetHost, internalHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Connecting to the internal document server database is prohibited.");
        }

        try
        {
            var internalIps = await ResolveAddressesAsync(internalHost, ct);
            var normalizedTargetSet = targetIps
                .Select(ip => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip)
                .ToHashSet();

            foreach (var internalIp in internalIps)
            {
                var normalizedInternal = internalIp.IsIPv4MappedToIPv6 ? internalIp.MapToIPv4() : internalIp;
                if (normalizedTargetSet.Contains(normalizedInternal))
                {
                    throw new ForbiddenException("Connecting to the internal document server database is prohibited.");
                }
            }
        }
        catch (DomainValidationException)
        {
            // Ignore unresolvable internal hostname during offline/unit test environments
        }
    }

    private static string SanitizeCommentsAndLiterals(string sql)
    {
        var sb = new StringBuilder(sql.Length);
        int i = 0;
        int len = sql.Length;

        while (i < len)
        {
            char c = sql[i];

            // Single-line comment: -- ... \n
            if (c == '-' && i + 1 < len && sql[i + 1] == '-')
            {
                sb.Append(' ');
                i += 2;
                while (i < len && sql[i] != '\n' && sql[i] != '\r')
                {
                    i++;
                }
                continue;
            }

            // Block comment: /* ... */
            if (c == '/' && i + 1 < len && sql[i + 1] == '*')
            {
                sb.Append(' ');
                i += 2;
                bool closed = false;
                while (i + 1 < len)
                {
                    if (sql[i] == '*' && sql[i + 1] == '/')
                    {
                        i += 2;
                        closed = true;
                        break;
                    }
                    i++;
                }

                if (!closed)
                {
                    throw new DomainValidationException("Unclosed block comment in SQL query.");
                }
                continue;
            }

            // Single-quoted string literal: '...' (with '' escape)
            if (c == '\'')
            {
                sb.Append("' '");
                i++;
                bool closed = false;
                while (i < len)
                {
                    if (sql[i] == '\'')
                    {
                        if (i + 1 < len && sql[i + 1] == '\'')
                        {
                            i += 2;
                            continue;
                        }
                        i++;
                        closed = true;
                        break;
                    }
                    i++;
                }

                if (!closed)
                {
                    throw new DomainValidationException("Unclosed string literal in SQL query.");
                }
                continue;
            }

            // PostgreSQL dollar-quoting ($tag$ or $$) is prohibited in read-only dataset queries
            if (c == '$')
            {
                int nextDollar = sql.IndexOf('$', i + 1);
                if (nextDollar > i)
                {
                    string between = sql[(i + 1)..nextDollar];
                    if (between.Length == 0 || between.All(ch => char.IsLetterOrDigit(ch) || ch == '_'))
                    {
                        throw new DomainValidationException("Dollar-quoted SQL strings are prohibited in dataset queries.");
                    }
                }
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }
}
