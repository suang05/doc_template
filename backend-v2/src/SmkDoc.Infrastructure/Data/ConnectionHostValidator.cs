using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Data;

/// <summary>
/// Consolidated outbound SSRF, DNS-rebinding, CGNAT/RFC1918 egress policy, and SQL read-only AST validator.
/// Resolves and pins validated target IP addresses so database and webhook transports connect directly without DNS TOCTOU.
/// </summary>
public sealed partial class ConnectionHostValidator(
    IConfiguration configuration,
    IOptions<EgressPolicyOptions> egressOptions,
    IProjectStatusReader? projectStatusReader = null,
    ISecurityAuditService? securityAuditService = null,
    Func<string, CancellationToken, Task<IPAddress[]>>? dnsResolver = null) : IConnectionHostValidator
{
    private static readonly IPNetwork LoopbackV4 = IPNetwork.Parse("127.0.0.0/8");
    private static readonly IPNetwork UnspecifiedV4 = IPNetwork.Parse("0.0.0.0/8");
    private static readonly IPNetwork LinkLocalV4 = IPNetwork.Parse("169.254.0.0/16");
    private static readonly IPNetwork CgnatV4 = IPNetwork.Parse("100.64.0.0/10");
    private static readonly IPNetwork MulticastV4 = IPNetwork.Parse("224.0.0.0/4");
    private static readonly IPNetwork ReservedV4 = IPNetwork.Parse("240.0.0.0/4");
    private static readonly IPNetwork ProtocolAssignmentsV4 = IPNetwork.Parse("192.0.0.0/24");
    private static readonly IPNetwork TestNet1V4 = IPNetwork.Parse("192.0.2.0/24");
    private static readonly IPNetwork BenchmarkingV4 = IPNetwork.Parse("198.18.0.0/15");
    private static readonly IPNetwork TestNet2V4 = IPNetwork.Parse("198.51.100.0/24");
    private static readonly IPNetwork TestNet3V4 = IPNetwork.Parse("203.0.113.0/24");

    private static readonly IPNetwork PrivateClassA = IPNetwork.Parse("10.0.0.0/8");
    private static readonly IPNetwork PrivateClassB = IPNetwork.Parse("172.16.0.0/12");
    private static readonly IPNetwork PrivateClassC = IPNetwork.Parse("192.168.0.0/16");

    private static readonly IPNetwork LoopbackV6 = IPNetwork.Parse("::1/128");
    private static readonly IPNetwork UnspecifiedV6 = IPNetwork.Parse("::/128");
    private static readonly IPNetwork LinkLocalV6 = IPNetwork.Parse("fe80::/10");
    private static readonly IPNetwork MulticastV6 = IPNetwork.Parse("ff00::/8");
    private static readonly IPNetwork DiscardV6 = IPNetwork.Parse("100::/64");
    private static readonly IPNetwork Nat64V6 = IPNetwork.Parse("64:ff9b::/96");
    private static readonly IPNetwork DocV6 = IPNetwork.Parse("2001:db8::/32");
    private static readonly IPNetwork UlaV6 = IPNetwork.Parse("fc00::/7");

    private static readonly HashSet<string> ProhibitedInternalHostnames = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "ip6-localhost",
        "ip6-loopback",
        "postgres",
        "postgresql",
        "minio",
        "redis",
        "gotenberg",
        "doc-server",
        "doc-server-v2",
        "doc-worker",
        "doc-worker-v2",
        "metadata",
        "metadata.google.internal",
        "metadata.internal",
        "host.docker.internal",
        "gateway.docker.internal"
    };

    private static readonly HashSet<string> MutatingOrDangerousKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "CREATE", "TRUNCATE",
        "MERGE", "UPSERT", "REPLACE", "GRANT", "REVOKE", "EXEC", "EXECUTE",
        "CALL", "DO", "COPY", "SET", "RESET", "VACUUM", "ANALYZE", "CLUSTER",
        "LOCK", "LISTEN", "NOTIFY", "PREPARE", "DEALLOCATE", "RETURNING",
        "PG_READ_FILE", "PG_READ_BINARY_FILE", "PG_LS_DIR", "PG_STAT_FILE",
        "PG_SLEEP", "PG_TERMINATE_BACKEND", "PG_CANCEL_BACKEND",
        "LO_IMPORT", "LO_EXPORT", "DBLINK", "DBLINK_EXEC",
        "XP_CMDSHELL", "OPENROWSET", "OPENDATASOURCE", "LOAD_FILE"
    };

    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _dnsResolver = dnsResolver ?? DefaultDnsResolveAsync;

    public async Task<ResolvedEgressTarget> ResolveAndValidateHostAsync(
        string host,
        int port,
        ProjectId? projectId,
        CancellationToken ct)
    {
        return await ResolveAndValidateInternalAsync(
            host,
            port,
            projectId,
            schemeOrProvider: "http",
            ct);
    }

    public async Task<ResolvedEgressTarget> ValidateConnectionTargetAsync(
        string provider,
        string connectionString,
        ProjectId? projectId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new DomainValidationException("Data connection provider is required.");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new DomainValidationException("Connection string cannot be empty.");
        }

        var (rawHost, port) = ExtractTargetHostAndPort(provider, connectionString);
        return await ResolveAndValidateInternalAsync(
            rawHost,
            port,
            projectId,
            provider,
            ct);
    }

    public void ValidateReadOnlySql(string sqlQuery)
    {
        if (string.IsNullOrWhiteSpace(sqlQuery))
        {
            throw new DomainValidationException("SQL query cannot be empty.");
        }

        string normalizedSql = StripCommentsAndValidateDelimiters(sqlQuery);
        if (string.IsNullOrWhiteSpace(normalizedSql))
        {
            throw new DomainValidationException("SQL query cannot contain only comments.");
        }

        string codeOnly = StripSingleAndDoubleQuotedLiterals(normalizedSql);

        int semicolonIdx = codeOnly.IndexOf(';', StringComparison.Ordinal);
        if (semicolonIdx >= 0 && !string.IsNullOrWhiteSpace(codeOnly[(semicolonIdx + 1)..]))
        {
            throw new DomainValidationException("Stacked or multi-statement SQL queries are strictly prohibited.");
        }

        var tokenMatches = SqlIdentifierRegex().Matches(codeOnly);
        if (tokenMatches.Count == 0)
        {
            throw new DomainValidationException("SQL query must start with SELECT or WITH.");
        }

        string firstKeyword = tokenMatches[0].Value;
        if (!string.Equals(firstKeyword, "SELECT", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(firstKeyword, "WITH", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainValidationException("Only read-only SELECT or WITH (CTE) queries are permitted.");
        }

        var forbiddenMatch = tokenMatches.Cast<Match>()
            .FirstOrDefault(m => MutatingOrDangerousKeywords.Contains(m.Value));
        if (forbiddenMatch is not null)
        {
            throw new DomainValidationException($"Prohibited SQL keyword or function detected: '{forbiddenMatch.Value}'.");
        }
    }

    private async Task<ResolvedEgressTarget> ResolveAndValidateInternalAsync(
        string rawHost,
        int port,
        ProjectId? projectId,
        string schemeOrProvider,
        CancellationToken ct)
    {
        if (port is < 1 or > 65535)
        {
            DenyAndAudit(projectId, schemeOrProvider, "invalid-port");
        }

        string host = NormalizeHost(rawHost);
        if (string.IsNullOrWhiteSpace(host) ||
            host.Contains(',', StringComparison.Ordinal) ||
            host.StartsWith('/'))
        {
            DenyAndAudit(projectId, schemeOrProvider, "invalid-host");
        }

        if (IsProhibitedInternalHostname(host) || await MatchesControlPlaneHostAsync(host, ct))
        {
            DenyAndAudit(projectId, schemeOrProvider, "prohibited-hostname");
        }

        var resolvedAddresses = await ResolveTargetAddressesAsync(host, projectId, schemeOrProvider, ct);
        var pinned = await FilterAndPinAddressesAsync(resolvedAddresses, projectId, schemeOrProvider, ct);
        return new ResolvedEgressTarget(host, pinned.AsReadOnly(), port);
    }

    private async Task<IPAddress[]> ResolveTargetAddressesAsync(
        string host,
        ProjectId? projectId,
        string schemeOrProvider,
        CancellationToken ct)
    {
        if (IPAddress.TryParse(host, out var literalIp))
        {
            return [literalIp];
        }

        IPAddress[] resolvedAddresses;
        try
        {
            resolvedAddresses = await _dnsResolver(host, ct);
        }
        catch (Exception ex) when (ex is SocketException or InvalidOperationException)
        {
            DenyAndAudit(projectId, schemeOrProvider, "dns-resolution-failed");
            return [];
        }

        if (resolvedAddresses.Length == 0)
        {
            DenyAndAudit(projectId, schemeOrProvider, "dns-empty");
        }

        return resolvedAddresses;
    }

    private async Task<List<IPAddress>> FilterAndPinAddressesAsync(
        IPAddress[] resolvedAddresses,
        ProjectId? projectId,
        string schemeOrProvider,
        CancellationToken ct)
    {
        var allowedPrivateNetworks = await LoadAllowedPrivateNetworksAsync(projectId, ct);
        var platformDbAddresses = await ResolvePlatformDatabaseAddressesAsync(ct);

        var pinned = new List<IPAddress>(resolvedAddresses.Length);
        foreach (var rawAddress in resolvedAddresses)
        {
            var address = NormalizeAddress(rawAddress);

            if (IsAlwaysProhibitedAddress(address, platformDbAddresses))
            {
                DenyAndAudit(projectId, schemeOrProvider, "prohibited-ip-range");
            }

            if (IsPrivateOrUlaAddress(address) && !IsExplicitlyAllowed(address, allowedPrivateNetworks))
            {
                DenyAndAudit(projectId, schemeOrProvider, "private-cidr-not-allowlisted");
            }

            if (!pinned.Contains(address))
            {
                pinned.Add(address);
            }
        }

        return pinned;
    }

    private async Task<List<IPNetwork>> LoadAllowedPrivateNetworksAsync(ProjectId? projectId, CancellationToken ct)
    {
        var result = new List<IPNetwork>();
        ParseAndAddCidrs(egressOptions.Value.TenantAllowedCidrs, result);

        if (projectId is { IsEmpty: false } && projectStatusReader is not null)
        {
            var snapshot = await projectStatusReader.FindProjectStatusAsync(projectId.Value, ct);
            if (snapshot is not null)
            {
                ParseAndAddCidrs(snapshot.AllowedEgressCidrs, result);
            }
        }

        return result;
    }

    private async Task<HashSet<IPAddress>> ResolvePlatformDatabaseAddressesAsync(CancellationToken ct)
    {
        var addresses = new HashSet<IPAddress>();
        foreach (var connKey in new[] { "ConnectionStrings:DefaultConnection", "ConnectionStrings:MigratorConnection" })
        {
            string? connStr = configuration[connKey];
            if (string.IsNullOrWhiteSpace(connStr))
            {
                continue;
            }

            string? host = TryExtractHostFromNpgsql(connStr);
            if (string.IsNullOrWhiteSpace(host))
            {
                continue;
            }

            if (IPAddress.TryParse(host, out var literal))
            {
                addresses.Add(NormalizeAddress(literal));
                continue;
            }

            try
            {
                var ips = await _dnsResolver(host, ct);
                foreach (var ip in ips)
                {
                    addresses.Add(NormalizeAddress(ip));
                }
            }
            catch (SocketException)
            {
                // Ignore DNS failures on non-resolvable dev host names; name match handles them.
            }
        }

        return addresses;
    }

    private Task<bool> MatchesControlPlaneHostAsync(string host, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        foreach (var connKey in new[] { "ConnectionStrings:DefaultConnection", "ConnectionStrings:MigratorConnection" })
        {
            string? connStr = configuration[connKey];
            string? connHost = TryExtractHostFromNpgsql(connStr);
            if (!string.IsNullOrWhiteSpace(connHost) &&
                string.Equals(NormalizeHost(connHost), host, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(true);
            }
        }

        foreach (var infraUrlKey in new[] { "Gotenberg:BaseUrl", "Minio:Endpoint", "ConnectionStrings:Redis" })
        {
            string? rawValue = configuration[infraUrlKey];
            string? infraHost = ExtractHostFromUrlOrEndpoint(rawValue);
            if (!string.IsNullOrWhiteSpace(infraHost) &&
                string.Equals(infraHost, host, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    private static bool IsProhibitedInternalHostname(string host)
    {
        if (ProhibitedInternalHostnames.Contains(host))
        {
            return true;
        }

        return host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAlwaysProhibitedAddress(IPAddress ip, HashSet<IPAddress> platformAddresses)
    {
        if (platformAddresses.Contains(ip) ||
            IPAddress.IsLoopback(ip) ||
            IPAddress.Any.Equals(ip) ||
            IPAddress.IPv6Any.Equals(ip) ||
            IPAddress.Broadcast.Equals(ip) ||
            ip.IsIPv6LinkLocal ||
            ip.IsIPv6Multicast ||
            ip.IsIPv6SiteLocal)
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return LoopbackV4.Contains(ip) ||
                   UnspecifiedV4.Contains(ip) ||
                   LinkLocalV4.Contains(ip) ||
                   CgnatV4.Contains(ip) ||
                   MulticastV4.Contains(ip) ||
                   ReservedV4.Contains(ip) ||
                   ProtocolAssignmentsV4.Contains(ip) ||
                   TestNet1V4.Contains(ip) ||
                   BenchmarkingV4.Contains(ip) ||
                   TestNet2V4.Contains(ip) ||
                   TestNet3V4.Contains(ip);
        }

        return LoopbackV6.Contains(ip) ||
               UnspecifiedV6.Contains(ip) ||
               LinkLocalV6.Contains(ip) ||
               MulticastV6.Contains(ip) ||
               DiscardV6.Contains(ip) ||
               Nat64V6.Contains(ip) ||
               DocV6.Contains(ip);
    }

    private static bool IsPrivateOrUlaAddress(IPAddress ip)
    {
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            return PrivateClassA.Contains(ip) ||
                   PrivateClassB.Contains(ip) ||
                   PrivateClassC.Contains(ip);
        }

        return UlaV6.Contains(ip);
    }

    private static bool IsExplicitlyAllowed(IPAddress ip, List<IPNetwork> allowedNetworks)
    {
        foreach (var network in allowedNetworks)
        {
            if (network.BaseAddress.AddressFamily == ip.AddressFamily && network.Contains(ip))
            {
                return true;
            }
        }

        return false;
    }

    private static void ParseAndAddCidrs(IEnumerable<string>? rawCidrs, List<IPNetwork> destination)
    {
        if (rawCidrs is null)
        {
            return;
        }

        foreach (var raw in rawCidrs)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (IPNetwork.TryParse(raw.Trim(), out var network))
            {
                var baseAddr = NormalizeAddress(network.BaseAddress);
                if (baseAddr.AddressFamily == AddressFamily.InterNetwork &&
                    network.BaseAddress.IsIPv4MappedToIPv6)
                {
                    int prefix = Math.Max(0, network.PrefixLength - 96);
                    destination.Add(new IPNetwork(baseAddr, prefix));
                }
                else
                {
                    destination.Add(network);
                }
            }
        }
    }

    private static (string Host, int Port) ExtractTargetHostAndPort(string provider, string connectionString)
    {
        if (string.Equals(provider, "PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString);
                if (string.IsNullOrWhiteSpace(builder.Host))
                {
                    throw new DomainValidationException("PostgreSQL connection string must specify Host.");
                }

                return (builder.Host, builder.Port);
            }
            catch (ArgumentException ex)
            {
                throw new DomainValidationException($"Invalid PostgreSQL connection string format: {ex.Message}");
            }
        }

        try
        {
            var generic = new DbConnectionStringBuilder { ConnectionString = connectionString };
            string? serverValue = TryGetKeyword(generic, "Server", "Data Source", "Host", "Addr", "Address", "Network Address");
            if (string.IsNullOrWhiteSpace(serverValue))
            {
                throw new DomainValidationException($"Connection string for provider '{provider}' must specify a host or server.");
            }

            string rawHost = serverValue.Trim();
            int defaultPort = provider.ToUpperInvariant() switch
            {
                "SQLSERVER" => 1433,
                "MYSQL" => 3306,
                "ORACLE" => 1521,
                _ => 5432
            };

            if (rawHost.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            {
                rawHost = rawHost[4..];
            }

            if (rawHost.Contains(',', StringComparison.Ordinal))
            {
                int commaIdx = rawHost.LastIndexOf(',');
                if (int.TryParse(rawHost[(commaIdx + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sqlPort))
                {
                    return (rawHost[..commaIdx], sqlPort);
                }
            }

            if (int.TryParse(TryGetKeyword(generic, "Port"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int explicitPort))
            {
                return (rawHost, explicitPort);
            }

            return (rawHost, defaultPort);
        }
        catch (ArgumentException ex)
        {
            throw new DomainValidationException($"Invalid connection string format: {ex.Message}");
        }
    }

    private static string? TryGetKeyword(DbConnectionStringBuilder builder, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (builder.TryGetValue(key, out var val) && val is not null)
            {
                return val.ToString();
            }
        }

        return null;
    }

    private static string? TryExtractHostFromNpgsql(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        try
        {
            return new NpgsqlConnectionStringBuilder(connectionString).Host;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? ExtractHostFromUrlOrEndpoint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        int commaIdx = trimmed.IndexOf(',', StringComparison.Ordinal);
        string firstEndpoint = commaIdx >= 0 ? trimmed[..commaIdx] : trimmed;
        int colonIdx = firstEndpoint.LastIndexOf(':');
        return colonIdx > 0 ? firstEndpoint[..colonIdx] : firstEndpoint;
    }

    private static string NormalizeHost(string rawHost)
    {
        string trimmed = rawHost.Trim();
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            trimmed = trimmed[1..^1];
        }

        return trimmed.TrimEnd('.');
    }

    private static IPAddress NormalizeAddress(IPAddress address) =>
        address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    private void DenyAndAudit(ProjectId? projectId, string schemeOrProvider, string reasonCategory)
    {
        securityAuditService?.LogEgressDenied(projectId?.Value, schemeOrProvider, reasonCategory);
        throw SecurityViolationException.EgressDenied();
    }

    private static string StripCommentsAndValidateDelimiters(string sql)
    {
        var sb = new StringBuilder(sql.Length);
        int i = 0;
        bool inSingleQuote = false;
        bool inDoubleQuote = false;

        while (i < sql.Length)
        {
            if (!inSingleQuote && !inDoubleQuote && TryConsumeCommentOrRejectDollarQuote(sql, ref i, sb))
            {
                continue;
            }

            if (TryConsumeEscapedQuote(sql, ref i, ref inSingleQuote, ref inDoubleQuote, sb, emitQuotes: true))
            {
                continue;
            }

            sb.Append(sql[i]);
            i++;
        }

        if (inSingleQuote || inDoubleQuote)
        {
            throw new DomainValidationException("SQL query contains an unclosed string or identifier quote.");
        }

        return sb.ToString();
    }

    private static bool TryConsumeCommentOrRejectDollarQuote(string sql, ref int index, StringBuilder sb)
    {
        char c = sql[index];
        char next = index + 1 < sql.Length ? sql[index + 1] : '\0';

        if (c == '-' && next == '-')
        {
            ConsumeLineComment(sql, ref index, sb);
            return true;
        }

        if (c == '/' && next == '*')
        {
            ConsumeBlockComment(sql, ref index, sb);
            return true;
        }

        if (c == '$' && FindDollarQuoteEnd(sql, index) > index)
        {
            throw new DomainValidationException("Dollar-quoted SQL strings or anonymous code blocks are prohibited.");
        }

        return false;
    }

    private static void ConsumeLineComment(string sql, ref int index, StringBuilder sb)
    {
        index += 2;
        while (index < sql.Length && sql[index] != '\n')
        {
            index++;
        }
        sb.Append(' ');
    }

    private static void ConsumeBlockComment(string sql, ref int index, StringBuilder sb)
    {
        index += 2;
        while (index + 1 < sql.Length)
        {
            if (sql[index] == '*' && sql[index + 1] == '/')
            {
                index += 2;
                sb.Append(' ');
                return;
            }
            index++;
        }

        throw new DomainValidationException("SQL block comment is not properly closed.");
    }

    private static bool TryConsumeEscapedQuote(
        string sql,
        ref int index,
        ref bool inSingleQuote,
        ref bool inDoubleQuote,
        StringBuilder sb,
        bool emitQuotes)
    {
        char c = sql[index];
        if (c == '\'' && !inDoubleQuote)
        {
            return TryConsumeQuoteToken(sql, '\'', ref index, ref inSingleQuote, sb, emitQuotes);
        }

        if (c == '"' && !inSingleQuote)
        {
            return TryConsumeQuoteToken(sql, '"', ref index, ref inDoubleQuote, sb, emitQuotes);
        }

        return false;
    }

    private static bool TryConsumeQuoteToken(
        string sql,
        char quoteChar,
        ref int index,
        ref bool inQuote,
        StringBuilder sb,
        bool emitQuotes)
    {
        char next = index + 1 < sql.Length ? sql[index + 1] : '\0';
        if (inQuote && next == quoteChar)
        {
            if (emitQuotes)
            {
                sb.Append(quoteChar).Append(quoteChar);
            }
            index += 2;
            return true;
        }

        inQuote = !inQuote;
        sb.Append(emitQuotes ? quoteChar : ' ');
        index++;
        return true;
    }

    private static int FindDollarQuoteEnd(string sql, int startIndex)
    {
        int i = startIndex + 1;
        while (i < sql.Length)
        {
            char c = sql[i];
            if (c == '$')
            {
                return i;
            }

            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return -1;
            }

            i++;
        }

        return -1;
    }

    private static string StripSingleAndDoubleQuotedLiterals(string sql)
    {
        var sb = new StringBuilder(sql.Length);
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        int i = 0;

        while (i < sql.Length)
        {
            if (TryConsumeEscapedQuote(sql, ref i, ref inSingleQuote, ref inDoubleQuote, sb, emitQuotes: false))
            {
                continue;
            }

            sb.Append(inSingleQuote || inDoubleQuote ? ' ' : sql[i]);
            i++;
        }

        return sb.ToString();
    }

    private static Task<IPAddress[]> DefaultDnsResolveAsync(string host, CancellationToken ct)
    {
#pragma warning disable RS0030 // ConnectionHostValidator IS the sanctioned DNS perimeter boundary replacing legacy direct Dns calls
        return Dns.GetHostAddressesAsync(host, ct);
#pragma warning restore RS0030
    }

    [GeneratedRegex(@"\b[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.CultureInvariant)]
    private static partial Regex SqlIdentifierRegex();
}
