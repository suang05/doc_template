using System.Net;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

public sealed record ResolvedEgressTarget(
    string NormalizedHost,
    IReadOnlyList<IPAddress> PinnedAddresses,
    int Port);

/// <summary>
/// Authoritative outbound SSRF &amp; DNS-rebinding egress security validator for SQL connections and webhooks.
/// Returns validated pinned IP addresses so connect-time handlers eliminate TOCTOU DNS rebinding.
/// </summary>
public interface IConnectionHostValidator
{
    Task<ResolvedEgressTarget> ResolveAndValidateHostAsync(string host, int port, ProjectId? projectId, CancellationToken ct);
    Task<ResolvedEgressTarget> ValidateConnectionTargetAsync(string provider, string connectionString, ProjectId? projectId, CancellationToken ct);
    void ValidateReadOnlySql(string sqlQuery);
}
