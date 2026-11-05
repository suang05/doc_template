using System.Text.RegularExpressions;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Infrastructure.Engines.Placeholders;

public static class HandlebarsPlaceholderScanner
{
    private static readonly Regex TokenRegex = new(
        @"\{\{\{?([^{}]+)\}?\}\}",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public static List<TemplatePlaceholder> Scan(string templateText)
    {
        if (string.IsNullOrWhiteSpace(templateText))
            return [];

        var results = new List<TemplatePlaceholder>();
        var collectionStack = new Stack<string>();
        var matches = TokenRegex.Matches(templateText);

        foreach (Match match in matches)
        {
            string raw = match.Groups[1].Value.Trim();

            // 1. Comments
            if (raw.StartsWith('!') || raw.StartsWith("!--"))
                continue;

            // 2. Block Closing
            if (raw.StartsWith('/'))
            {
                string blockName = raw[1..].Trim();
                if (blockName.Equals("each", StringComparison.OrdinalIgnoreCase) && collectionStack.Count > 0)
                {
                    collectionStack.Pop();
                }
                continue;
            }

            // 3. Block Opening: #each
            if (raw.StartsWith("#each", StringComparison.OrdinalIgnoreCase))
            {
                string rest = raw[5..].Trim();
                // Handle optional " as |item|" or " this"
                string collectionName = rest.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                if (!string.IsNullOrWhiteSpace(collectionName) && !collectionName.Equals("this", StringComparison.OrdinalIgnoreCase))
                {
                    collectionStack.Push(collectionName);
                    results.Add(new TemplatePlaceholder(collectionName, PlaceholderCategory.Tabular, null, "array"));
                }
                continue;
            }

            // 4. Block Opening: #if / #unless
            if (raw.StartsWith("#if", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("#unless", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1 && !PlaceholderCategorizer.IsControlKeyword(parts[1]))
                {
                    string varName = parts[1];
                    results.Add(PlaceholderCategorizer.Categorize(varName, collectionStack.Count > 0 ? collectionStack.Peek() : null, "boolean"));
                }
                continue;
            }

            // 5. Inverted / Else blocks
            if (raw.StartsWith('^') || raw.Equals("else", StringComparison.OrdinalIgnoreCase))
                continue;

            // 6. Regular expressions
            string? currentCollection = collectionStack.Count > 0 ? collectionStack.Peek() : null;
            var placeholder = PlaceholderCategorizer.Categorize(raw, currentCollection);

            if (!string.IsNullOrWhiteSpace(placeholder.Name) && !PlaceholderCategorizer.IsControlKeyword(placeholder.Name))
            {
                results.Add(placeholder);
            }
        }

        // Distinct by Name & Category & ParentCollection
        return results
            .GroupBy(p => (p.Name, p.Category, p.ParentCollection))
            .Select(g => g.First())
            .ToList();
    }
}
