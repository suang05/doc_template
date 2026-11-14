using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Infrastructure.Engines.Placeholders;

public static class PlaceholderCategorizer
{
    private static readonly HashSet<string> DateHelpers = new(StringComparer.OrdinalIgnoreCase)
    {
        "formatDate", "date", "thaidate", "thaibuddhistdate"
    };

    private static readonly HashSet<string> NumberHelpers = new(StringComparer.OrdinalIgnoreCase)
    {
        "formatCurrency", "currency", "formatNumber", "numberFormat", "bahttext", "addOne", "increment", "sum"
    };

    private static readonly HashSet<string> SystemIdentifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "@index", "@first", "@last", "@key", "@root", "pageNumber", "totalPages", "page", "pages"
    };

    private static readonly HashSet<string> ControlKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "else", "this", "true", "false", "null", "undefined"
    };

    public static bool IsControlKeyword(string token) =>
        ControlKeywords.Contains(token);

    public static bool IsSystemIdentifier(string token) =>
        token.StartsWith('@') || SystemIdentifiers.Contains(token);

    public static (string VariableName, string InferredType) ParseHelperInvocation(string rawExpression)
    {
        var parts = rawExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return (rawExpression, "string");

        string helper = parts[0];
        string targetVar = parts[1].Trim('"', '\'');

        if (DateHelpers.Contains(helper))
            return (targetVar, "date");

        if (NumberHelpers.Contains(helper))
            return (targetVar, "number");

        return (targetVar, "string");
    }

    public static TemplatePlaceholder Categorize(
        string rawExpression,
        string? currentCollection = null,
        string? forcedType = null)
    {
        string expr = rawExpression.Trim();

        // 1. Helper invocation check (e.g., "formatDate orderDate 'dd/MM/yyyy'", "addOne @index")
        string inferredType = forcedType ?? "string";
        if (expr.Contains(' ', StringComparison.Ordinal))
        {
            var (variableName, helperType) = ParseHelperInvocation(expr);
            expr = variableName;
            if (forcedType == null)
            {
                inferredType = helperType;
            }
        }

        // 2. System variable (e.g. "@index", "pageNumber", "@first")
        if (IsSystemIdentifier(expr))
        {
            return TemplatePlaceholder.System(expr);
        }

        // Clean parent traversal if any (e.g. "../orderId")
        if (expr.StartsWith("../", StringComparison.Ordinal))
        {
            string clean = expr.Replace("../", string.Empty, StringComparison.Ordinal);
            return clean.Contains('.', StringComparison.Ordinal)
                ? TemplatePlaceholder.Object(clean, "object")
                : TemplatePlaceholder.Scalar(clean, inferredType);
        }

        // Clean "this." prefix
        if (expr.StartsWith("this.", StringComparison.OrdinalIgnoreCase))
        {
            expr = expr[5..];
        }

        // 3. Tabular (within repeating collection)
        if (!string.IsNullOrWhiteSpace(currentCollection))
        {
            return TemplatePlaceholder.Tabular(expr, currentCollection, inferredType);
        }

        // 4. Nested Object
        if (expr.Contains('.', StringComparison.Ordinal))
        {
            return TemplatePlaceholder.Object(expr, "object");
        }

        // 5. Scalar
        return TemplatePlaceholder.Scalar(expr, inferredType);
    }
}

