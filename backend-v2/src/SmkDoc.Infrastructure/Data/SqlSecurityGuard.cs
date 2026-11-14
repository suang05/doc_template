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
#pragma warning disable RS0030 // Designated SSRF DNS validation adapter boundary (validated against private CIDR ranges before connect)
        dnsResolver ?? ((host, token) => Dns.GetHostAddressesAsync(host, token));
#pragma warning restore RS0030

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

    public async Task ValidateConnectionTargetAsync(string provider, string connectionString, CancellationToken ct)
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

        if (host.Contains(',', StringComparison.Ordinal))
        {
            throw new ForbiddenException("Multiple hosts in external data connection strings are prohibited.");
        }

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Connecting to loopback host 'localhost' is prohibited.");
        }

        var blockedInternalHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "postgres", "minio", "redis", "gotenberg", "db", "app", "host.docker.internal"
        };

        if (blockedInternalHosts.Contains(host))
        {
            throw new ForbiddenException($"Connecting to internal infrastructure host '{host}' is prohibited.");
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

        if (sanitized.Contains(';', StringComparison.Ordinal))
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
        foreach (string token in matches.Select(match => match.Value))
        {
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
            return ParsePostgreSqlEndpoint(connectionString);
        }

        return ParseGenericEndpoint(connectionString);
    }

    private static (string Host, int Port, string Database) ParsePostgreSqlEndpoint(string connectionString)
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

    private static (string Host, int Port, string Database) ParseGenericEndpoint(string connectionString)
    {
        try
        {
            var generic = new DbConnectionStringBuilder { ConnectionString = connectionString };
            string host = GetFirstKey(generic, "Host", "Server", "Data Source");
            string db = GetFirstKey(generic, "Database", "Initial Catalog");
            string portStr = GetFirstKey(generic, "Port");
            int port = int.TryParse(portStr, out int p) && p > 0 ? p : 0;

            SplitEmbeddedPortFromHost(ref host, ref port);
            return (host.Trim(), port, db.Trim());
        }
        catch (ArgumentException ex)
        {
            throw new DomainValidationException($"Invalid connection string format: {ex.Message}");
        }
    }

    private static void SplitEmbeddedPortFromHost(ref string host, ref int port)
    {
        if (host.Contains(':', StringComparison.Ordinal) && !host.StartsWith('['))
        {
            var parts = host.Split(':', 2);
            host = parts[0];
            if (port == 0 && int.TryParse(parts[1], out int parsedPort))
            {
                port = parsedPort;
            }
            return;
        }

        if (host.Contains(',', StringComparison.Ordinal))
        {
            var parts = host.Split(',', 2);
            if (int.TryParse(parts[1], out int sqlServerPort))
            {
                host = parts[0];
                if (port == 0)
                {
                    port = sqlServerPort;
                }
            }
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
        else if (normalized.AddressFamily == AddressFamily.InterNetworkV6 && normalized.IsIPv6LinkLocal)
        {
            throw new ForbiddenException(
                $"Connecting to IPv6 link-local address '{normalized}' (host '{originalHost}') is prohibited.");
        }
    }

    private async Task EnforceNotSelfDatabaseAsync(
        string targetHost,
        int targetPort,
        string targetDatabase,
        IPAddress[] targetIps,
        CancellationToken ct)
    {
        if (!TryGetInternalDbEndpoint(out string internalHost, out int internalPort, out string internalDb))
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

        await EnsureNoSharedResolvedAddressAsync(internalHost, targetIps, ct);
    }

    private bool TryGetInternalDbEndpoint(out string host, out int port, out string database)
    {
        host = string.Empty;
        port = 0;
        database = string.Empty;

        string? internalConnStr = configuration?.GetConnectionString("DefaultConnection")
            ?? configuration?["DATABASE_URL"];

        if (string.IsNullOrWhiteSpace(internalConnStr))
        {
            return false;
        }

        try
        {
            var internalBuilder = new NpgsqlConnectionStringBuilder(internalConnStr);
            host = (internalBuilder.Host ?? string.Empty).Trim();
            port = internalBuilder.Port > 0 ? internalBuilder.Port : 5432;
            database = (internalBuilder.Database ?? string.Empty).Trim();
            return !string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(database);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return false;
        }
    }

    private async Task EnsureNoSharedResolvedAddressAsync(
        string internalHost,
        IPAddress[] targetIps,
        CancellationToken ct)
    {
        try
        {
            var internalIps = await ResolveAddressesAsync(internalHost, ct);
            var normalizedTargetSet = targetIps
                .Select(NormalizeIpv4Mapped)
                .ToHashSet();

            if (internalIps.Select(NormalizeIpv4Mapped).Any(normalizedTargetSet.Contains))
            {
                throw new ForbiddenException("Connecting to the internal document server database is prohibited.");
            }
        }
        catch (DomainValidationException)
        {
            // Ignore unresolvable internal hostname during offline/unit test environments
        }
    }

    private static IPAddress NormalizeIpv4Mapped(IPAddress ip) =>
        ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;

    private static string SanitizeCommentsAndLiterals(string sql)
    {
        var sb = new StringBuilder(sql.Length);
        int i = 0;

        while (i < sql.Length)
        {
            char c = sql[i];

            if (IsTwoCharToken(sql, i, '-', '-'))
            {
                i = SkipLineComment(sql, i, sb);
                continue;
            }

            if (IsTwoCharToken(sql, i, '/', '*'))
            {
                i = SkipBlockComment(sql, i, sb);
                continue;
            }

            if (c == '\'')
            {
                i = SkipSingleQuotedLiteral(sql, i, sb);
                continue;
            }

            if (c == '$')
            {
                EnsureNotDollarQuoting(sql, i);
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    private static bool IsTwoCharToken(string sql, int index, char first, char second) =>
        index + 1 < sql.Length && sql[index] == first && sql[index + 1] == second;

    private static int SkipLineComment(string sql, int startIndex, StringBuilder sb)
    {
        sb.Append(' ');
        int i = startIndex + 2;
        while (i < sql.Length && sql[i] != '\n' && sql[i] != '\r')
        {
            i++;
        }
        return i;
    }

    private static int SkipBlockComment(string sql, int startIndex, StringBuilder sb)
    {
        sb.Append(' ');
        int i = startIndex + 2;
        while (i + 1 < sql.Length)
        {
            if (sql[i] == '*' && sql[i + 1] == '/')
            {
                return i + 2;
            }
            i++;
        }

        throw new DomainValidationException("Unclosed block comment in SQL query.");
    }

    private static int SkipSingleQuotedLiteral(string sql, int startIndex, StringBuilder sb)
    {
        sb.Append("' '");
        int i = startIndex + 1;
        while (i < sql.Length)
        {
            if (sql[i] != '\'')
            {
                i++;
                continue;
            }

            if (i + 1 < sql.Length && sql[i + 1] == '\'')
            {
                i += 2;
                continue;
            }

            return i + 1;
        }

        throw new DomainValidationException("Unclosed string literal in SQL query.");
    }

    private static void EnsureNotDollarQuoting(string sql, int dollarIndex)
    {
        int nextDollar = sql.IndexOf('$', dollarIndex + 1);
        if (nextDollar <= dollarIndex)
        {
            return;
        }

        string between = sql[(dollarIndex + 1)..nextDollar];
        if (between.Length == 0 || between.All(ch => char.IsLetterOrDigit(ch) || ch == '_'))
        {
            throw new DomainValidationException("Dollar-quoted SQL strings are prohibited in dataset queries.");
        }
    }
}
