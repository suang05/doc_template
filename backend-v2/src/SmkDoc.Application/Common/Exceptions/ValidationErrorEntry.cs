using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace SmkDoc.Application.Common.Exceptions;

/// <summary>
/// Normalized RFC 9457 field-level validation error entry with RFC 6901 JSON Pointer.
/// </summary>
public sealed record ValidationErrorEntry(
    [param: SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "RFC 9457 / RFC 6901 JSON Pointer field name.")]
    string Pointer,
    string Code,
    string Message)
{
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "RFC 9457 / RFC 6901 JSON Pointer field name.")]
    public string Pointer { get; init; } = Pointer;

    private const string DefaultValidationCode = "VALIDATION_ERROR";

    /// <summary>
    /// Converts a FluentValidation property path (e.g. <c>mappings[0].datasetId</c>) or JSON path
    /// (<c>$.customer.taxId</c>, <c>#/tax_id</c>) into an RFC 6901 JSON Pointer.
    /// </summary>
    public static string ToJsonPointer(string? rawPath, bool camelCaseSegments = false)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return string.Empty;
        }

        var trimmed = rawPath.Trim();
        if (trimmed is "$" or "$." or "#")
        {
            return string.Empty;
        }

        if (trimmed == "/")
        {
            return "/";
        }

        if (trimmed.StartsWith("#/", StringComparison.Ordinal))
        {
            return trimmed[1..];
        }

        if (trimmed.StartsWith('/'))
        {
            return trimmed;
        }

        if (trimmed.StartsWith("$.", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..];
        }
        else if (trimmed.StartsWith('$'))
        {
            trimmed = trimmed[1..];
        }

        var segments = new List<string>();
        foreach (var rawSegment in trimmed.Split(['.', '[', ']'], StringSplitOptions.RemoveEmptyEntries))
        {
            var segment = camelCaseSegments ? ToCamelCaseSegment(rawSegment) : rawSegment;
            segments.Add(EscapeJsonPointerToken(segment));
        }

        return segments.Count == 0
            ? string.Empty
            : "/" + string.Join('/', segments);
    }

    /// <summary>
    /// Normalizes a validator or JSON schema rule identifier (e.g. <c>NotEmptyValidator</c>, <c>minLength</c>)
    /// to an uppercase snake-case machine code.
    /// </summary>
    public static string NormalizeErrorCode(string? rawCode, string fallback = DefaultValidationCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
        {
            return fallback;
        }

        var span = rawCode.Trim();
        if (IsAlreadyUpperSnakeCase(span))
        {
            return span;
        }

        const string validatorSuffix = "Validator";
        if (span.Length > validatorSuffix.Length &&
            span.EndsWith(validatorSuffix, StringComparison.Ordinal))
        {
            span = span[..^validatorSuffix.Length];
        }

        return span switch
        {
            "MinimumLength" or "MinLength" or "minLength" => "MIN_LENGTH",
            "MaximumLength" or "MaxLength" or "maxLength" => "MAX_LENGTH",
            "RegularExpression" => "INVALID_FORMAT",
            _ => ConvertToUpperSnakeCase(span, fallback)
        };
    }

    private static string ToCamelCaseSegment(string segment)
    {
        if (string.IsNullOrEmpty(segment) || !char.IsUpper(segment[0]))
        {
            return segment;
        }

        return string.Create(segment.Length, segment, static (chars, source) =>
        {
            source.CopyTo(chars);
            chars[0] = char.ToLowerInvariant(chars[0]);
        });
    }

    private static string EscapeJsonPointerToken(string token) =>
        token.Contains('~', StringComparison.Ordinal) || token.Contains('/', StringComparison.Ordinal)
            ? token.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal)
            : token;

    private static bool IsAlreadyUpperSnakeCase(string value)
    {
        var hasLetter = false;
        foreach (var ch in value)
        {
            if (char.IsAsciiLetterUpper(ch))
            {
                hasLetter = true;
            }
            else if (!char.IsAsciiDigit(ch) && ch != '_')
            {
                return false;
            }
        }

        return hasLetter;
    }

    private static string ConvertToUpperSnakeCase(string input, string fallback)
    {
        var sb = new StringBuilder(input.Length + 4);
        var previousWasUnderscore = false;

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            if (ch is '-' or '_' or '.' or ' ')
            {
                if (sb.Length > 0 && !previousWasUnderscore)
                {
                    sb.Append('_');
                    previousWasUnderscore = true;
                }

                continue;
            }

            if (char.IsUpper(ch) && i > 0 && !previousWasUnderscore && char.IsLower(input[i - 1]))
            {
                sb.Append('_');
            }

            sb.Append(char.ToUpperInvariant(ch));
            previousWasUnderscore = false;
        }

        var result = sb.ToString().Trim('_');
        return string.IsNullOrEmpty(result) ? fallback : result;
    }
}
