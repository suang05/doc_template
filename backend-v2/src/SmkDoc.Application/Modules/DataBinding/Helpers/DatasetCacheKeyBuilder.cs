using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;

namespace SmkDoc.Application.Modules.DataBinding.Helpers;

/// <summary>
/// Stateless utility for building deterministic, collision-resistant cache keys for dataset execution results.
/// </summary>
public static class DatasetCacheKeyBuilder
{
    public static string BuildKey(
        Guid? projectId,
        string alias,
        ResolvedDatasetContext ds,
        IReadOnlyDictionary<string, object> parameters)
    {
        var tenantSegment = projectId.HasValue && projectId.Value != Guid.Empty
            ? projectId.Value.ToString("N")
            : "unscoped";

        var datasetSegment = ds.DatasetId != Guid.Empty
            ? ds.DatasetId.ToString("N")
            : alias.ToUpperInvariant();

        var connectionSegment = ds.DataConnectionId != Guid.Empty
            ? ds.DataConnectionId.ToString("N")
            : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ds.ConnectionString)))[..16];

        var sb = new StringBuilder(256);
        sb.Append(ds.Provider).Append('\n').Append(ds.SqlQuery);
        foreach (var kvp in parameters.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.Append('\n')
              .Append(kvp.Key.ToUpperInvariant())
              .Append('=')
              .Append(kvp.Value is DBNull ? "<null>" : Convert.ToString(kvp.Value, CultureInfo.InvariantCulture));
        }

        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
        return $"dataset:{tenantSegment}:{datasetSegment}:{connectionSegment}:{fingerprint}";
    }
}

