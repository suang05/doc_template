using System.Text.RegularExpressions;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Infrastructure.Engines.Placeholders;

public static partial class HandlebarsPlaceholderScanner
{
    [GeneratedRegex(@"\{\{\{?([^{}]+)\}?\}\}", RegexOptions.Singleline, 1000)]
    private static partial Regex TokenRegex();

    public static IReadOnlyList<TemplatePlaceholder> Scan(string templateText)
    {
        if (string.IsNullOrWhiteSpace(templateText))
            return [];

        var results = new List<TemplatePlaceholder>();
        var collectionStack = new Stack<string>();

        foreach (Match match in TokenRegex().Matches(templateText))
        {
            ProcessToken(match.Groups[1].Value.Trim(), collectionStack, results);
        }

        // Distinct by Name & Category & ParentCollection
        return results
            .GroupBy(p => (p.Name, p.Category, p.ParentCollection))
            .Select(g => g.First())
            .ToList();
    }

    private static void ProcessToken(
        string raw,
        Stack<string> collectionStack,
        List<TemplatePlaceholder> results)
    {
        if (IsIgnoredToken(raw))
            return;

        if (raw.StartsWith('/'))
        {
            HandleBlockClose(raw, collectionStack);
            return;
        }

        if (raw.StartsWith("#each", StringComparison.OrdinalIgnoreCase))
        {
            HandleEachBlock(raw, collectionStack, results);
            return;
        }

        if (raw.StartsWith("#if", StringComparison.OrdinalIgnoreCase) ||
            raw.StartsWith("#unless", StringComparison.OrdinalIgnoreCase))
        {
            HandleConditionalBlock(raw, collectionStack, results);
            return;
        }

        AddStandardPlaceholder(raw, collectionStack, results);
    }

    private static bool IsIgnoredToken(string raw) =>
        raw.StartsWith('!') || raw.StartsWith('^') || raw.Equals("else", StringComparison.OrdinalIgnoreCase);

    private static void HandleBlockClose(string raw, Stack<string> collectionStack)
    {
        string blockName = raw[1..].Trim();
        if (blockName.Equals("each", StringComparison.OrdinalIgnoreCase) && collectionStack.Count > 0)
        {
            collectionStack.Pop();
        }
    }

    private static void HandleEachBlock(
        string raw,
        Stack<string> collectionStack,
        List<TemplatePlaceholder> results)
    {
        string rest = raw[5..].Trim();
        // Handle optional " as |item|" or " this"
        string collectionName = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
        if (!string.IsNullOrWhiteSpace(collectionName) && !collectionName.Equals("this", StringComparison.OrdinalIgnoreCase))
        {
            collectionStack.Push(collectionName);
            results.Add(new TemplatePlaceholder(collectionName, PlaceholderCategory.Tabular, null, "array"));
        }
    }

    private static void HandleConditionalBlock(
        string raw,
        Stack<string> collectionStack,
        List<TemplatePlaceholder> results)
    {
        string[] parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1 && !PlaceholderCategorizer.IsControlKeyword(parts[1]))
        {
            string? currentCollection = collectionStack.Count > 0 ? collectionStack.Peek() : null;
            results.Add(PlaceholderCategorizer.Categorize(parts[1], currentCollection, "boolean"));
        }
    }

    private static void AddStandardPlaceholder(
        string raw,
        Stack<string> collectionStack,
        List<TemplatePlaceholder> results)
    {
        string? currentCollection = collectionStack.Count > 0 ? collectionStack.Peek() : null;
        var placeholder = PlaceholderCategorizer.Categorize(raw, currentCollection);

        if (!string.IsNullOrWhiteSpace(placeholder.Name) && !PlaceholderCategorizer.IsControlKeyword(placeholder.Name))
        {
            results.Add(placeholder);
        }
    }
}
