using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SmkDoc.Api.Filters;

/// <summary>
/// Computes deterministic SHA-256 fingerprints of HTTP mutation requests to detect payload tampering.
/// </summary>
public static class RequestFingerprintCalculator
{
    public static string ComputeFingerprint(ActionExecutingContext context, JsonSerializerOptions jsonSerializerOptions)
    {
        var httpMethod = context.HttpContext.Request.Method.ToUpperInvariant();
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;

        var normalizedArguments = new SortedDictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (name, value) in context.ActionArguments)
        {
            if (value is CancellationToken)
            {
                continue;
            }

            if (value is IFormFile formFile)
            {
                normalizedArguments[name] = new
                {
                    fileName = formFile.FileName,
                    length = formFile.Length,
                    contentType = formFile.ContentType
                };
            }
            else
            {
                normalizedArguments[name] = value;
            }
        }

        var serializedPayload = JsonSerializer.Serialize(normalizedArguments, jsonSerializerOptions);
        var canonicalString = $"{httpMethod}|{path}|{serializedPayload}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalString));

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
