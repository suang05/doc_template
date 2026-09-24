using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Application.Common.Helpers;

public sealed class MathExpressionResolverService : IMathExpressionResolver
{
    // Matches bare identifiers: letter/underscore followed by word chars
    private static readonly Regex IdentifierPattern =
        new(@"[A-Za-z_]\w*", RegexOptions.Compiled);

    public string Resolve(string expression, IReadOnlyDictionary<string, string?> variables)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return string.Empty;

        // Substitute known numeric variables; leave unknown tokens intact
        string substituted = IdentifierPattern.Replace(expression, m =>
        {
            string token = m.Value;
            if (variables.TryGetValue(token, out string? val)
                && !string.IsNullOrWhiteSpace(val)
                && decimal.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                return $"({val})";
            }
            return token;
        });

        try
        {
            object? result = new DataTable().Compute(substituted, null);
            if (result is null || result == DBNull.Value)
                return string.Empty;

            if (decimal.TryParse(result.ToString(), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out decimal dec))
            {
                // G10 trims trailing zeros (1.50 → 1.5, 2.00 → 2)
                return dec.ToString("G10", CultureInfo.InvariantCulture);
            }

            return result.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
