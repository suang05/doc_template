using System.Text.RegularExpressions;
using Microsoft.OpenApi;

namespace SmkDoc.Api.OpenApi;

/// <summary>
/// Guarantees <c>operationId</c> uniqueness (OAS requirement) when a single action is exposed
/// under several route templates (aliases). The canonical route — shortest path — keeps the base id;
/// each alias gets a deterministic suffix derived from its path.
/// </summary>
public static partial class OpenApiOperationIdDeduplicator
{
    public static void Apply(OpenApiDocument document)
    {
        // Step 1: Collect every operation that already has an id
        var operations = CollectOperations(document);

        // Step 2: Rename aliases inside each colliding group
        var collisions = operations
            .GroupBy(entry => entry.Operation.OperationId!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1);

        foreach (var group in collisions)
        {
            RenameAliases(group);
        }
    }

    private static List<OperationEntry> CollectOperations(OpenApiDocument document)
    {
        var entries = new List<OperationEntry>();
        foreach (var (path, pathItem) in document.Paths)
        {
            foreach (var (method, operation) in pathItem.Operations ?? [])
            {
                if (!string.IsNullOrEmpty(operation.OperationId))
                {
                    entries.Add(new OperationEntry(path, method.Method, operation));
                }
            }
        }

        return entries;
    }

    private static void RenameAliases(IEnumerable<OperationEntry> group)
    {
        var ordered = group
            .OrderBy(entry => entry.Path.Length)
            .ThenBy(entry => entry.Path, StringComparer.Ordinal)
            .ThenBy(entry => entry.Method, StringComparer.Ordinal)
            .ToList();

        foreach (var alias in ordered.Skip(1))
        {
            alias.Operation.OperationId = $"{alias.Operation.OperationId}_{BuildPathSuffix(alias.Path)}";
        }
    }

    /// <summary>"/api/v1/management/settings/fonts" → "ManagementSettingsFonts".</summary>
    public static string BuildPathSuffix(string path)
    {
        var tokens = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => !IsApiPrefixSegment(segment))
            .SelectMany(segment => NonAlphanumeric().Split(segment))
            .Where(token => token.Length > 0)
            .Select(token => char.ToUpperInvariant(token[0]) + token[1..]);

        return string.Concat(tokens);
    }

    private static bool IsApiPrefixSegment(string segment) =>
        segment.Equals("api", StringComparison.OrdinalIgnoreCase) || VersionSegment().IsMatch(segment);

    [GeneratedRegex("[^A-Za-z0-9]+")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex("^v[0-9]+$", RegexOptions.IgnoreCase)]
    private static partial Regex VersionSegment();

    private sealed record OperationEntry(string Path, string Method, OpenApiOperation Operation);
}
