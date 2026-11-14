using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Parsing;

namespace SmkDoc.Infrastructure.Schema;

/// <summary>
/// Infrastructure adapter providing strict Standard JSON Schema (Draft-07) inference
/// and mock data generation for HTML, Word (OpenXML), and Excel (ClosedXML) templates.
/// </summary>
public sealed partial class SchemaInferenceService(TimeProvider timeProvider) : ISchemaInferenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly HashSet<string> SkipHtmlTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "addOne", "inc", "this", "else", "if", "each", "ifEquals"
    };

    public string InferSchemaFromPlaceholders(
        IEnumerable<string> placeholders,
        string? samplePayloadJson = null,
        string? templateSlug = null)
    {
        var grouped = GroupPlaceholders(placeholders, explicitArrayKeys: null);
        return BuildDraft07Schema(grouped, templateSlug);
    }

    public string GenerateDefaultSamplePayload(IEnumerable<string> placeholders)
    {
        var grouped = GroupPlaceholders(placeholders, explicitArrayKeys: null);
        return BuildSamplePayloadJson(grouped, timeProvider.GetUtcNow());
    }

    public (string DataSchema, string SamplePayload) InferFromHtml(
        string htmlContent,
        string? customSamplePayloadJson = null,
        string? templateSlug = null)
    {
        var (tokens, explicitArrayKeys) = ExtractTokensFromHtml(htmlContent ?? string.Empty);
        var grouped = GroupPlaceholders(tokens, explicitArrayKeys);

        string defaultPayload = BuildSamplePayloadJson(grouped, timeProvider.GetUtcNow());
        string finalPayload = MergeWithCustomPayload(defaultPayload, customSamplePayloadJson);
        string schema = BuildDraft07Schema(grouped, templateSlug);

        return (schema, finalPayload);
    }

    // ─── Token Extraction & Classification ──────────────────────────────────────

    private sealed record GroupedTokens(
        List<string> Scalars,
        Dictionary<string, List<string>> Objects,
        Dictionary<string, List<string>> Arrays);

    private static (List<string> Tokens, HashSet<string> ArrayKeys) ExtractTokensFromHtml(string htmlContent)
    {
        var arrayGroups = ExtractArrayGroupTokens(htmlContent);
        var scalarPlaceholders = ExtractScalarTokens(htmlContent, arrayGroups);
        var allTokens = CombineExtractedTokens(scalarPlaceholders, arrayGroups);

        return (allTokens, new HashSet<string>(arrayGroups.Keys, StringComparer.OrdinalIgnoreCase));
    }

    private static Dictionary<string, HashSet<string>> ExtractArrayGroupTokens(string htmlContent)
    {
        var arrayGroups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var eachMatches = EachBlockRegex().Matches(htmlContent);

        foreach (GroupCollection groups in eachMatches.Select(m => m.Groups))
        {
            string arrayKey = groups[1].Value.Trim();
            string innerHtml = groups[2].Value;

            if (!arrayGroups.TryGetValue(arrayKey, out var group))
            {
                group = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                arrayGroups[arrayKey] = group;
            }

            AddMatchingPlaceholders(innerHtml, group);
        }

        return arrayGroups;
    }

    private static void AddMatchingPlaceholders(string htmlFragment, HashSet<string> destination)
    {
        foreach (Match im in TemplatePlaceholderParser.Pattern.Matches(htmlFragment))
        {
            string tag = im.Groups[1].Value.Trim();
            if (!IsControlTag(tag) && !SkipHtmlTags.Contains(tag))
            {
                destination.Add(tag);
            }
        }
    }

    private static HashSet<string> ExtractScalarTokens(
        string htmlContent,
        Dictionary<string, HashSet<string>> arrayGroups)
    {
        var scalarPlaceholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match pm in TemplatePlaceholderParser.Pattern.Matches(htmlContent))
        {
            string tag = pm.Groups[1].Value.Trim();
            if (IsControlTag(tag) || SkipHtmlTags.Contains(tag)) continue;

            var parsed = TemplatePlaceholderParser.Parse(tag);
            if (!arrayGroups.ContainsKey(parsed.Key))
            {
                scalarPlaceholders.Add(tag);
            }
        }

        return scalarPlaceholders;
    }

    private static List<string> CombineExtractedTokens(
        HashSet<string> scalarPlaceholders,
        Dictionary<string, HashSet<string>> arrayGroups)
    {
        var allTokens = new List<string>(scalarPlaceholders);
        foreach (var (arrKey, subFields) in arrayGroups)
        {
            if (subFields.Count == 0)
            {
                allTokens.Add($"{arrKey}.name");
                continue;
            }

            foreach (var sf in subFields)
            {
                allTokens.Add($"{arrKey}.{sf}");
            }
        }

        return allTokens;
    }

    private static bool IsControlTag(string tag) =>
        tag.StartsWith('#') || tag.StartsWith('/') || tag.StartsWith('@') || tag.StartsWith('^');

    private static GroupedTokens GroupPlaceholders(IEnumerable<string> placeholders, HashSet<string>? explicitArrayKeys)
    {
        var scalars = new List<string>();
        var objects = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var arrays = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var ph in placeholders)
        {
            if (string.IsNullOrWhiteSpace(ph)) continue;
            var parsed = TemplatePlaceholderParser.Parse(ph);

            if (!parsed.Key.Contains('.', StringComparison.Ordinal))
            {
                scalars.Add(ph);
                continue;
            }

            var parts = parsed.Key.Split('.', 2);
            string groupKey = parts[0].Trim();
            string fieldKey = parts[1].Trim();
            string itemPh = string.IsNullOrEmpty(parsed.Extra) ? fieldKey : $"{fieldKey}:{parsed.Extra}";

            bool isArray = explicitArrayKeys != null
                ? explicitArrayKeys.Contains(groupKey)
                : IsCollectionName(groupKey);

            var targetDict = isArray ? arrays : objects;
            if (!targetDict.TryGetValue(groupKey, out var list))
            {
                list = [];
                targetDict[groupKey] = list;
            }
            list.Add(itemPh);
        }

        return new GroupedTokens(scalars, objects, arrays);
    }

    // ─── Draft-07 Schema Assembly ───────────────────────────────────────────────

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "JSON Schema $id URI path uses canonical lowercase template slug.")]
    private static string BuildDraft07Schema(GroupedTokens grouped, string? templateSlug)
    {
        var rootProperties = new JsonObject();
        var requiredFields = new JsonArray();

        // 1. Scalar properties
        foreach (var ph in grouped.Scalars)
        {
            var parsed = TemplatePlaceholderParser.Parse(ph);
            if (rootProperties.ContainsKey(parsed.Key)) continue;

            rootProperties[parsed.Key] = InferPropertySchema(parsed.Key, parsed.Extra, parsed.Kind);
            requiredFields.Add(parsed.Key);
        }

        // 2. Nested objects
        foreach (var (objKey, subFields) in grouped.Objects)
        {
            var (itemProps, itemReq) = BuildChildProperties(subFields);
            rootProperties[objKey] = new JsonObject
            {
                ["type"] = "object",
                ["description"] = $"Information for {objKey}",
                ["required"] = itemReq,
                ["properties"] = itemProps,
                ["additionalProperties"] = false
            };
            requiredFields.Add(objKey);
        }

        // 3. Array collections
        foreach (var (arrayKey, itemPlaceholders) in grouped.Arrays)
        {
            var (itemProps, itemReq) = BuildChildProperties(itemPlaceholders);
            rootProperties[arrayKey] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = $"List of {arrayKey} entries",
                ["minItems"] = 1,
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["required"] = itemReq,
                    ["properties"] = itemProps,
                    ["additionalProperties"] = false
                }
            };
            requiredFields.Add(arrayKey);
        }

        string idSlug = string.IsNullOrWhiteSpace(templateSlug) ? "document" : templateSlug.Trim().ToLowerInvariant();
        string titleText = string.IsNullOrWhiteSpace(templateSlug) ? "DocumentDataContract" : $"{ToPascalCase(templateSlug)}DataContract";

        var schema = new JsonObject
        {
            ["$schema"] = "http://json-schema.org/draft-07/schema#",
            ["$id"] = $"https://api.sammakorn.co.th/schemas/{idSlug}.json",
            ["title"] = titleText,
            ["description"] = "Generated JSON Schema Draft-07 contract for document generation",
            ["type"] = "object",
            ["properties"] = rootProperties,
            ["required"] = requiredFields,
            ["additionalProperties"] = true
        };

        return schema.ToJsonString(JsonOptions);
    }

    private static (JsonObject Properties, JsonArray Required) BuildChildProperties(IEnumerable<string> subPlaceholders)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var ph in subPlaceholders)
        {
            var parsed = TemplatePlaceholderParser.Parse(ph);
            if (properties.ContainsKey(parsed.Key)) continue;

            properties[parsed.Key] = InferPropertySchema(parsed.Key, parsed.Extra, parsed.Kind);
            required.Add(parsed.Key);
        }

        return (properties, required);
    }

    // ─── Sample Payload Generation ──────────────────────────────────────────────

    private static string BuildSamplePayloadJson(GroupedTokens grouped, DateTimeOffset now)
    {
        var root = new JsonObject();

        // 1. Scalars
        foreach (var ph in grouped.Scalars)
        {
            var parsed = TemplatePlaceholderParser.Parse(ph);
            if (!root.ContainsKey(parsed.Key))
            {
                root[parsed.Key] = GenerateMockValue(parsed.Key, parsed.Extra, parsed.Kind, now);
            }
        }

        // 2. Nested objects
        foreach (var (objKey, subFields) in grouped.Objects)
        {
            var nested = new JsonObject();
            foreach (var subPh in subFields)
            {
                var parsed = TemplatePlaceholderParser.Parse(subPh);
                if (!nested.ContainsKey(parsed.Key))
                {
                    nested[parsed.Key] = GenerateMockValue(parsed.Key, parsed.Extra, parsed.Kind, now);
                }
            }
            root[objKey] = nested;
        }

        // 3. Arrays
        foreach (var (arrayKey, itemPlaceholders) in grouped.Arrays)
        {
            var arr = new JsonArray();
            for (int i = 1; i <= 2; i++)
            {
                var itemObj = new JsonObject();
                foreach (var itemPh in itemPlaceholders)
                {
                    var parsed = TemplatePlaceholderParser.Parse(itemPh);
                    itemObj[parsed.Key] = GenerateItemMockValue(parsed.Key, parsed.Extra, i);
                }
                arr.Add(itemObj);
            }
            root[arrayKey] = arr;
        }

        return root.ToJsonString(JsonOptions);
    }

    private static JsonValue GenerateItemMockValue(string key, string? extra, int index)
    {
        if (key.Equals("no", StringComparison.OrdinalIgnoreCase) || key.Equals("seq", StringComparison.OrdinalIgnoreCase))
            return JsonValue.Create(index.ToString(System.Globalization.CultureInfo.InvariantCulture));

        if (IsIntegerKey(key, extra))
            return JsonValue.Create(index);

        if (IsNumericKey(key, extra))
            return JsonValue.Create((decimal)(index * 1500));

        return JsonValue.Create($"ตัวอย่าง {key} {index}");
    }

    private static string MergeWithCustomPayload(string defaultPayload, string? customPayloadJson)
    {
        if (string.IsNullOrWhiteSpace(customPayloadJson) || customPayloadJson.Trim() == "{}")
            return defaultPayload;

        try
        {
            var customNode = JsonNode.Parse(customPayloadJson)?.AsObject();
            var defaultNode = JsonNode.Parse(defaultPayload)?.AsObject();

            if (customNode != null && defaultNode != null)
            {
                foreach (var (key, value) in defaultNode)
                {
                    if (!customNode.ContainsKey(key))
                    {
                        customNode[key] = value?.DeepClone();
                    }
                }
                return customNode.ToJsonString(JsonOptions);
            }
        }
        catch (JsonException)
        {
            // Fallback on invalid JSON
        }

        return defaultPayload;
    }

    // ─── Semantic Type Inference & Mock Generators ──────────────────────────────

    private static JsonObject InferPropertySchema(string key, string? extra, PlaceholderKind kind)
    {
        var obj = new JsonObject();
        switch (kind)
        {
            case PlaceholderKind.Qr:
                obj["type"] = "string";
                obj["format"] = "uri";
                obj["description"] = "QR Code target URL or URI";
                return obj;

            case PlaceholderKind.Barcode:
                obj["type"] = "string";
                obj["pattern"] = "^[A-Za-z0-9-_]+$";
                obj["description"] = "Barcode alphanumeric code string";
                return obj;

            case PlaceholderKind.Image:
                obj["type"] = "string";
                obj["description"] = "Image URL or Base64 data URI";
                return obj;
        }

        if (IsThaiIdentityKey(key))
        {
            obj["type"] = "string";
            obj["pattern"] = "^[0-9]{13}$";
            obj["description"] = "13-digit Thai tax or citizen identification number";
            return obj;
        }

        if (key.Contains("email", StringComparison.OrdinalIgnoreCase))
        {
            obj["type"] = "string";
            obj["format"] = "email";
            obj["description"] = "Valid email address";
            return obj;
        }

        if (key.Contains("tel", StringComparison.OrdinalIgnoreCase) || key.Contains("phone", StringComparison.OrdinalIgnoreCase) || key.Contains("mobile", StringComparison.OrdinalIgnoreCase) || key.Contains("fax", StringComparison.OrdinalIgnoreCase))
        {
            obj["type"] = "string";
            obj["pattern"] = @"^[0-9+()\-\s]{9,20}$";
            obj["description"] = "Contact telephone or mobile number";
            return obj;
        }

        if (IsDateKey(key, extra))
        {
            obj["type"] = "string";
            obj["format"] = "date";
            obj["description"] = $"Date value for {key} (YYYY-MM-DD)";
            return obj;
        }

        if (key.Contains("time", StringComparison.OrdinalIgnoreCase) && !key.Contains("date", StringComparison.OrdinalIgnoreCase))
        {
            obj["type"] = "string";
            obj["format"] = "time";
            obj["description"] = $"Time value for {key} (HH:mm:ss)";
            return obj;
        }

        if (IsIntegerKey(key, extra))
        {
            obj["type"] = "integer";
            obj["minimum"] = (key.ToUpperInvariant() is "QTY" or "QUANTITY" or "NO" or "SEQ" or "ITEM_NO") ? 1 : 0;
            obj["description"] = $"Integer quantity or sequence for {key}";
            return obj;
        }

        if (IsNumericKey(key, extra))
        {
            obj["type"] = "number";
            obj["minimum"] = 0;
            obj["description"] = $"Numeric amount or price for {key}";
            return obj;
        }

        // Default string constraint
        obj["type"] = "string";
        obj["minLength"] = 1;
        obj["description"] = $"Text value for {key}";
        return obj;
    }

    private static JsonValue GenerateMockValue(string key, string? extra, PlaceholderKind kind, DateTimeOffset now)
    {
        switch (kind)
        {
            case PlaceholderKind.Qr:
                return JsonValue.Create("https://sammakorn.co.th");
            case PlaceholderKind.Barcode:
                return JsonValue.Create("SMK-998822");
            case PlaceholderKind.Image:
                return JsonValue.Create("https://placehold.co/300x200/png");
        }

        if (IsThaiIdentityKey(key)) return JsonValue.Create("0107536000123");
        if (IsIntegerKey(key, extra)) return JsonValue.Create(1);
        if (IsNumericKey(key, extra)) return JsonValue.Create(250000.00m);
        if (IsDateKey(key, extra)) return JsonValue.Create(now.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        if (key.Contains("email", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create("contact@sammakorn.co.th");
        if (key.Contains("phone", StringComparison.OrdinalIgnoreCase) || key.Contains("tel", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create("02-123-4567");
        if (key.Contains("doc_no", StringComparison.OrdinalIgnoreCase) || key.Contains("inv_no", StringComparison.OrdinalIgnoreCase) || key.Contains("contract_no", StringComparison.OrdinalIgnoreCase) || key.Contains("receipt_no", StringComparison.OrdinalIgnoreCase))
            return JsonValue.Create($"INV-{now.UtcDateTime:yyyy}-0001");
        if (key.Contains("address", StringComparison.OrdinalIgnoreCase))
            return JsonValue.Create("123/45 ถนนพัฒนาการ แขวงสวนหลวง กรุงเทพมหานคร 10250");
        if (key.Contains("name", StringComparison.OrdinalIgnoreCase))
            return JsonValue.Create("บริษัท สัมมากร จำกัด (มหาชน)");

        return JsonValue.Create($"ตัวอย่าง {key}");
    }

    // ─── Classification Predicates ──────────────────────────────────────────────

    private static bool IsThaiIdentityKey(string key) =>
        key.ToUpperInvariant() is "TAX_ID" or "TAXID" or "CITIZEN_ID" or "CITIZENID" or "IDCARD" or "ID_CARD"
           or "IDENTIFICATION_NO" or "PERSONAL_ID";

    private static bool IsIntegerKey(string key, string? extra)
    {
        if (!string.IsNullOrEmpty(extra) && extra.ToUpperInvariant() is "INT" or "INTEGER" or "COUNT")
            return true;

        return key.ToUpperInvariant() is "QTY" or "QUANTITY" or "COUNT" or "SEQ" or "NO" or "ITEM_NO" or "UNIT" or "AGE";
    }

    private static bool IsNumericKey(string key, string? extra)
    {
        if (IsIntegerKey(key, extra)) return false;

        if (!string.IsNullOrEmpty(extra) &&
            extra.ToUpperInvariant() is "NUMBER" or "CURRENCY" or "CURRENCY0" or "BAHT" or "THAIBAHT" or "THAI_BAHT_TEXT" or "BAHT_TEXT" or "BAHTTEXT" or "DECIMAL")
            return true;

        return key.Contains("amount", StringComparison.OrdinalIgnoreCase) || key.Contains("price", StringComparison.OrdinalIgnoreCase) || key.Contains("total", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("cost", StringComparison.OrdinalIgnoreCase) || key.Contains("rate", StringComparison.OrdinalIgnoreCase) || key.Contains("vat", StringComparison.OrdinalIgnoreCase) || key.Contains("tax", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("fee", StringComparison.OrdinalIgnoreCase) || key.Contains("discount", StringComparison.OrdinalIgnoreCase) || key.Contains("sum", StringComparison.OrdinalIgnoreCase) || key.Contains("subtotal", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("balance", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDateKey(string key, string? extra)
    {
        if (!string.IsNullOrEmpty(extra) &&
            (extra.Contains("date", StringComparison.OrdinalIgnoreCase) || extra.Contains("time", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return key.Contains("date", StringComparison.OrdinalIgnoreCase) || key.Contains("time", StringComparison.OrdinalIgnoreCase) || key.Contains("day", StringComparison.OrdinalIgnoreCase) || key.Contains("month", StringComparison.OrdinalIgnoreCase) || key.Contains("year", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCollectionName(string key)
    {
        var uk = key.ToUpperInvariant();
        if (uk is "ITEMS" or "LINES" or "DETAILS" or "PRODUCTS" or "SERVICES" or "ROWS" or "ENTRIES" or "RECORDS" or "TRANSACTIONS" or "LIST")
            return true;

        if (uk.EndsWith("_LIST", StringComparison.Ordinal) || uk.EndsWith("_ITEMS", StringComparison.Ordinal) || uk.EndsWith("_DETAILS", StringComparison.Ordinal) || uk.EndsWith("_ROWS", StringComparison.Ordinal) || uk.EndsWith("_LINES", StringComparison.Ordinal) || uk.EndsWith("_RECORDS", StringComparison.Ordinal))
            return true;

        if (uk is "CUSTOMER" or "BUYER" or "SELLER" or "VENDOR" or "COMPANY" or "RECIPIENT" or "ISSUER"
               or "PAYER" or "PAYEE" or "APPROVER" or "APPLICANT" or "TENANT" or "LANDLORD" or "OWNER"
               or "CLIENT" or "HEADER" or "FOOTER" or "META" or "CONFIG" or "CONTACT" or "SENDER" or "RECEIVER"
               or "USER" or "PROFILE")
            return false;

        return uk.EndsWith('S');
    }

    private static string ToPascalCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Document";
        var words = NonAlphanumericRegex().Split(text);
        var sb = new StringBuilder();
        foreach (var w in words)
        {
            if (string.IsNullOrEmpty(w)) continue;
            sb.Append(char.ToUpperInvariant(w[0]));
            for (int i = 1; i < w.Length; i++) sb.Append(char.ToLowerInvariant(w[i]));
        }
        return sb.Length > 0 ? sb.ToString() : "Document";
    }

    [GeneratedRegex(@"\{\{#each\s+([a-zA-Z0-9_.]+)\s*\}\}([\s\S]*?)\{\{/each\}\}", RegexOptions.None, 500)]
    private static partial Regex EachBlockRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9]+", RegexOptions.None, 500)]
    private static partial Regex NonAlphanumericRegex();
}
