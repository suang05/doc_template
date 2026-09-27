using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Schema;

/// <summary>
/// Infrastructure adapter providing strict Standard JSON Schema (Draft-07) inference
/// and mock data generation for HTML, Word (OpenXML), and Excel (ClosedXML) templates.
/// </summary>
public partial class SchemaInferenceService : ISchemaInferenceService
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
        return BuildSamplePayloadJson(grouped);
    }

    public (string DataSchema, string SamplePayload) InferFromHtml(
        string htmlContent,
        string? customSamplePayloadJson = null,
        string? templateSlug = null)
    {
        var (tokens, explicitArrayKeys) = ExtractTokensFromHtml(htmlContent ?? string.Empty);
        var grouped = GroupPlaceholders(tokens, explicitArrayKeys);

        string defaultPayload = BuildSamplePayloadJson(grouped);
        string finalPayload = MergeWithCustomPayload(defaultPayload, customSamplePayloadJson);
        string schema = BuildDraft07Schema(grouped, templateSlug);

        return (schema, finalPayload);
    }

    // ─── Token Extraction & Classification ──────────────────────────────────────

    private record GroupedTokens(
        List<string> Scalars,
        Dictionary<string, List<string>> Objects,
        Dictionary<string, List<string>> Arrays);

    private static (List<string> Tokens, HashSet<string> ArrayKeys) ExtractTokensFromHtml(string htmlContent)
    {
        var scalarPlaceholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var arrayGroups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        // 1. Extract {{#each items}} loop blocks
        var eachMatches = EachBlockRegex().Matches(htmlContent);
        foreach (Match m in eachMatches)
        {
            string arrayKey = m.Groups[1].Value.Trim();
            string innerHtml = m.Groups[2].Value;

            if (!arrayGroups.TryGetValue(arrayKey, out var group))
            {
                group = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                arrayGroups[arrayKey] = group;
            }

            foreach (Match im in PlaceholderHelper.Pattern.Matches(innerHtml))
            {
                string tag = im.Groups[1].Value.Trim();
                if (IsControlTag(tag) || SkipHtmlTags.Contains(tag)) continue;
                group.Add(tag);
            }
        }

        // 2. Extract standard placeholders outside loop blocks
        foreach (Match pm in PlaceholderHelper.Pattern.Matches(htmlContent))
        {
            string tag = pm.Groups[1].Value.Trim();
            if (IsControlTag(tag) || SkipHtmlTags.Contains(tag)) continue;

            var parsed = PlaceholderHelper.Parse(tag);
            if (!arrayGroups.ContainsKey(parsed.Key))
            {
                scalarPlaceholders.Add(tag);
            }
        }

        // 3. Combine into unified tokens list
        var allTokens = new List<string>(scalarPlaceholders);
        foreach (var (arrKey, subFields) in arrayGroups)
        {
            if (subFields.Count == 0)
            {
                allTokens.Add($"{arrKey}.name");
            }
            else
            {
                foreach (var sf in subFields)
                    allTokens.Add($"{arrKey}.{sf}");
            }
        }

        return (allTokens, new HashSet<string>(arrayGroups.Keys, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsControlTag(string tag) =>
        tag.StartsWith('#') || tag.StartsWith('/') || tag.StartsWith('@') || tag.StartsWith('^');

    private static GroupedTokens GroupPlaceholders(IEnumerable<string> placeholders, ISet<string>? explicitArrayKeys)
    {
        var scalars = new List<string>();
        var objects = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var arrays = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var ph in placeholders)
        {
            if (string.IsNullOrWhiteSpace(ph)) continue;
            var parsed = PlaceholderHelper.Parse(ph);

            if (!parsed.Key.Contains('.'))
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

    private static string BuildDraft07Schema(GroupedTokens grouped, string? templateSlug)
    {
        var rootProperties = new JsonObject();
        var requiredFields = new JsonArray();

        // 1. Scalar properties
        foreach (var ph in grouped.Scalars)
        {
            var parsed = PlaceholderHelper.Parse(ph);
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
            var parsed = PlaceholderHelper.Parse(ph);
            if (properties.ContainsKey(parsed.Key)) continue;

            properties[parsed.Key] = InferPropertySchema(parsed.Key, parsed.Extra, parsed.Kind);
            required.Add(parsed.Key);
        }

        return (properties, required);
    }

    // ─── Sample Payload Generation ──────────────────────────────────────────────

    private static string BuildSamplePayloadJson(GroupedTokens grouped)
    {
        var root = new JsonObject();

        // 1. Scalars
        foreach (var ph in grouped.Scalars)
        {
            var parsed = PlaceholderHelper.Parse(ph);
            if (!root.ContainsKey(parsed.Key))
            {
                root[parsed.Key] = GenerateMockValue(parsed.Key, parsed.Extra, parsed.Kind);
            }
        }

        // 2. Nested objects
        foreach (var (objKey, subFields) in grouped.Objects)
        {
            var nested = new JsonObject();
            foreach (var subPh in subFields)
            {
                var parsed = PlaceholderHelper.Parse(subPh);
                if (!nested.ContainsKey(parsed.Key))
                {
                    nested[parsed.Key] = GenerateMockValue(parsed.Key, parsed.Extra, parsed.Kind);
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
                    var parsed = PlaceholderHelper.Parse(itemPh);
                    itemObj[parsed.Key] = GenerateItemMockValue(parsed.Key, parsed.Extra, i);
                }
                arr.Add(itemObj);
            }
            root[arrayKey] = arr;
        }

        return root.ToJsonString(JsonOptions);
    }

    private static JsonNode GenerateItemMockValue(string key, string? extra, int index)
    {
        if (key.Equals("no", StringComparison.OrdinalIgnoreCase) || key.Equals("seq", StringComparison.OrdinalIgnoreCase))
            return JsonValue.Create(index.ToString());

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
        catch
        {
            // Fallback on invalid JSON
        }

        return defaultPayload;
    }

    // ─── Semantic Type Inference & Mock Generators ──────────────────────────────

    private static JsonObject InferPropertySchema(string key, string? extra, PlaceholderHelper.PlaceholderKind kind)
    {
        var obj = new JsonObject();
        string lk = key.ToLowerInvariant();

        switch (kind)
        {
            case PlaceholderHelper.PlaceholderKind.Qr:
                obj["type"] = "string";
                obj["format"] = "uri";
                obj["description"] = "QR Code target URL or URI";
                return obj;

            case PlaceholderHelper.PlaceholderKind.Barcode:
                obj["type"] = "string";
                obj["pattern"] = "^[A-Za-z0-9-_]+$";
                obj["description"] = "Barcode alphanumeric code string";
                return obj;

            case PlaceholderHelper.PlaceholderKind.Image:
                obj["type"] = "string";
                obj["description"] = "Image URL or Base64 data URI";
                return obj;
        }

        if (IsThaiIdentityKey(lk))
        {
            obj["type"] = "string";
            obj["pattern"] = "^[0-9]{13}$";
            obj["description"] = "13-digit Thai tax or citizen identification number";
            return obj;
        }

        if (lk.Contains("email"))
        {
            obj["type"] = "string";
            obj["format"] = "email";
            obj["description"] = "Valid email address";
            return obj;
        }

        if (lk.Contains("tel") || lk.Contains("phone") || lk.Contains("mobile") || lk.Contains("fax"))
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

        if (lk.Contains("time") && !lk.Contains("date"))
        {
            obj["type"] = "string";
            obj["format"] = "time";
            obj["description"] = $"Time value for {key} (HH:mm:ss)";
            return obj;
        }

        if (IsIntegerKey(key, extra))
        {
            obj["type"] = "integer";
            obj["minimum"] = (lk is "qty" or "quantity" or "no" or "seq" or "item_no") ? 1 : 0;
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

    private static JsonNode GenerateMockValue(string key, string? extra, PlaceholderHelper.PlaceholderKind kind)
    {
        switch (kind)
        {
            case PlaceholderHelper.PlaceholderKind.Qr:
                return JsonValue.Create("https://sammakorn.co.th");
            case PlaceholderHelper.PlaceholderKind.Barcode:
                return JsonValue.Create("SMK-998822");
            case PlaceholderHelper.PlaceholderKind.Image:
                return JsonValue.Create("https://placehold.co/300x200/png");
        }

        string lk = key.ToLowerInvariant();

        if (IsThaiIdentityKey(lk)) return JsonValue.Create("0107536000123");
        if (IsIntegerKey(key, extra)) return JsonValue.Create(1);
        if (IsNumericKey(key, extra)) return JsonValue.Create(250000.00m);
        if (IsDateKey(key, extra)) return JsonValue.Create(DateTime.UtcNow.ToString("yyyy-MM-dd"));
        if (lk.Contains("email")) return JsonValue.Create("contact@sammakorn.co.th");
        if (lk.Contains("phone") || lk.Contains("tel")) return JsonValue.Create("02-123-4567");
        if (lk.Contains("doc_no") || lk.Contains("inv_no") || lk.Contains("contract_no") || lk.Contains("receipt_no"))
            return JsonValue.Create($"INV-{DateTime.UtcNow:yyyy}-0001");
        if (lk.Contains("address"))
            return JsonValue.Create("123/45 ถนนพัฒนาการ แขวงสวนหลวง กรุงเทพมหานคร 10250");
        if (lk.Contains("name"))
            return JsonValue.Create("บริษัท สัมมากร จำกัด (มหาชน)");

        return JsonValue.Create($"ตัวอย่าง {key}");
    }

    // ─── Classification Predicates ──────────────────────────────────────────────

    private static bool IsThaiIdentityKey(string lk) =>
        lk is "tax_id" or "taxid" or "citizen_id" or "citizenid" or "idcard" or "id_card"
           or "identification_no" or "personal_id";

    private static bool IsIntegerKey(string key, string? extra)
    {
        if (!string.IsNullOrEmpty(extra) && extra.ToLowerInvariant() is "int" or "integer" or "count")
            return true;

        string lk = key.ToLowerInvariant();
        return lk is "qty" or "quantity" or "count" or "seq" or "no" or "item_no" or "unit" or "age";
    }

    private static bool IsNumericKey(string key, string? extra)
    {
        if (IsIntegerKey(key, extra)) return false;

        if (!string.IsNullOrEmpty(extra) &&
            extra.ToLowerInvariant() is "number" or "currency" or "currency0" or "baht" or "thaibaht" or "thai_baht_text" or "baht_text" or "bahttext" or "decimal")
            return true;

        string lk = key.ToLowerInvariant();
        return lk.Contains("amount") || lk.Contains("price") || lk.Contains("total") ||
               lk.Contains("cost") || lk.Contains("rate") || lk.Contains("vat") || lk.Contains("tax") ||
               lk.Contains("fee") || lk.Contains("discount") || lk.Contains("sum") || lk.Contains("subtotal") ||
               lk.Contains("balance");
    }

    private static bool IsDateKey(string key, string? extra)
    {
        if (!string.IsNullOrEmpty(extra))
        {
            string ext = extra.ToLowerInvariant();
            if (ext.Contains("date") || ext.Contains("time")) return true;
        }

        string lk = key.ToLowerInvariant();
        return lk.Contains("date") || lk.Contains("time") || lk.Contains("day") || lk.Contains("month") || lk.Contains("year");
    }

    private static bool IsCollectionName(string key)
    {
        string lk = key.ToLowerInvariant();
        if (lk is "items" or "lines" or "details" or "products" or "services" or "rows" or "entries" or "records" or "transactions" or "list")
            return true;

        if (lk.EndsWith("_list") || lk.EndsWith("_items") || lk.EndsWith("_details") || lk.EndsWith("_rows") || lk.EndsWith("_lines") || lk.EndsWith("_records"))
            return true;

        if (lk is "customer" or "buyer" or "seller" or "vendor" or "company" or "recipient" or "issuer"
               or "payer" or "payee" or "approver" or "applicant" or "tenant" or "landlord" or "owner"
               or "client" or "header" or "footer" or "meta" or "config" or "contact" or "sender" or "receiver"
               or "user" or "profile")
            return false;

        return lk.EndsWith("s");
    }

    private static string ToPascalCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Document";
        var words = Regex.Split(text, @"[^a-zA-Z0-9]+");
        var sb = new StringBuilder();
        foreach (var w in words)
        {
            if (string.IsNullOrEmpty(w)) continue;
            sb.Append(char.ToUpperInvariant(w[0]));
            if (w.Length > 1) sb.Append(w[1..].ToLowerInvariant());
        }
        return sb.Length > 0 ? sb.ToString() : "Document";
    }

    [GeneratedRegex(@"\{\{#each\s+([a-zA-Z0-9_.]+)\s*\}\}([\s\S]*?)\{\{/each\}\}")]
    private static partial Regex EachBlockRegex();
}
