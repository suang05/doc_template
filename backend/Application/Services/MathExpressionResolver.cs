using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;

namespace SmkDocServer.Application.Services;

/// <summary>
/// Evaluates math expressions like "qty * price" against a variable dictionary.
/// Uses System.Data.DataTable.Compute — supports +, -, *, /, (, ), IIF, IsNull operators only.
/// Note: Math functions (ROUND, ABS, etc.) are NOT supported by DataTable.Compute.
/// </summary>
public static class MathExpressionResolver
{
    private static readonly Regex _identifierPattern =
        new(@"\b([a-zA-Z_]\w*)\b", RegexOptions.Compiled);

    /// <summary>
    /// Substitutes variable names in <paramref name="expression"/> with their resolved values
    /// from <paramref name="variables"/>, then evaluates the resulting arithmetic expression.
    /// </summary>
    /// <param name="expression">e.g. "qty * price" or "total - discount"</param>
    /// <param name="variables">current Replace dictionary from DocumentProcessingData</param>
    /// <returns>Formatted result string, or the original expression if evaluation fails</returns>
    public static string Evaluate(string expression, IReadOnlyDictionary<string, string?> variables)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return string.Empty;

        // Substitute each identifier token that matches a known variable
        string substituted = _identifierPattern.Replace(expression, match =>
        {
            string token = match.Value;
            if (variables.TryGetValue(token, out string? val) &&
                !string.IsNullOrWhiteSpace(val) &&
                decimal.TryParse(val, System.Globalization.NumberStyles.Any,
                                 System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                // Wrap in parens to preserve operator precedence
                return $"({val})";
            }
            // Leave non-numeric variables intact — DataTable.Compute will handle them or throw
            return token;
        });

        try
        {
            var dt = new DataTable();
            object? result = dt.Compute(substituted, null);

            if (result == null || result == DBNull.Value)
                return string.Empty;

            // Return as decimal with up to 10 significant decimal places, trimmed of trailing zeros
            if (decimal.TryParse(result.ToString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal dec))
            {
                return dec.ToString("G10", System.Globalization.CultureInfo.InvariantCulture);
            }

            return result.ToString() ?? string.Empty;
        }
        catch
        {
            // Return substituted expression on failure so document still renders
            return substituted;
        }
    }
}
