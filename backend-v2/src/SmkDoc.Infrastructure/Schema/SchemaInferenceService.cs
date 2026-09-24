using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SmkDoc.Application.Common.Helpers;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Schema;

/// <summary>
/// Infrastructure adapter providing strict Standard JSON Schema (Draft-07) inference and smart mock data generation
/// for HTML (Handlebars), Word (OpenXML), and Excel (ClosedXML) templates.
/// </summary>
public class SchemaInferenceService : ISchemaInferenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string InferSchemaFromPlaceholders(IEnumerable<string> placeholders, string? samplePayloadJson = null, string? templateSlug = null)
    {
        return InferSchemaInternal(placeholders, explicitArrayKeys: null, samplePayloadJson, templateSlug);
    }

    public string GenerateDefaultSamplePayload(IEnumerable<string> placeholders)
    {
        return GenerateDefaultSamplePayloadInternal(placeholders, explicitArrayKeys: null);
    }

    public (string DataSchema, string SamplePayload) InferFromHtml(string htmlContent, string? customSamplePayloadJson = null, string? templateSlug = null)
    {
        var scalarPlaceholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var arrayGroups = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        // 1. Extract {{#each items}} blocks
        var eachRegex = new Regex(@"\{\{#each\s+([a-zA-Z0-9_.]+)\s*\}\}([\s\S]*?)\{\{/each\}\}", RegexOptions.Compiled);
        var eachMatches = eachRegex.Matches(htmlContent ?? string.Empty);
        foreach (Match m in eachMatches)
        {
            string arrayKey = m.Groups[1].Value.Trim();
            string inner = m.Groups[2].Value;

            if (!arrayGroups.ContainsKey(arrayKey))
                arrayGroups[arrayKey] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var innerMatches = PlaceholderHelper.Pattern.Matches(inner);
            foreach (Match im in innerMatches)
            {
                string tag = im.Groups[1].Value.Trim();
                if (tag.StartsWith('#') || tag.StartsWith('/') || tag.StartsWith('@') || tag.StartsWith('^')) continue;
                if (new[] { "addOne", "inc", "this", "else", "if" }.Contains(tag)) continue;

                arrayGroups[arrayKey].Add(tag);
            }
        }

        var explicitArrayKeys = new HashSet<string>(arrayGroups.Keys, StringComparer.OrdinalIgnoreCase);

        // 2. Extract standard placeholders outside loop blocks
        var plainMatches = PlaceholderHelper.Pattern.Matches(htmlContent ?? string.Empty);
        foreach (Match pm in plainMatches)
        {
            string tag = pm.Groups[1].Value.Trim();
            if (tag.StartsWith('#') || tag.StartsWith('/') || tag.StartsWith('@') || tag.StartsWith('^')) continue;
            if (new[] { "addOne", "inc", "this", "else", "if", "each", "ifEquals" }.Contains(tag)) continue;

            var parsed = PlaceholderHelper.Parse(tag);
            if (arrayGroups.ContainsKey(parsed.Key)) continue;

            scalarPlaceholders.Add(tag);
        }

        // Combine into virtual tokens for unified processing
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

        string defaultPayload = GenerateDefaultSamplePayloadInternal(allTokens, explicitArrayKeys);
        string finalPayload = defaultPayload;

        // If custom payload provided, try merging
        if (!string.IsNullOrWhiteSpace(customSamplePayloadJson) && customSamplePayloadJson.Trim() != "{}")
        {
            try
            {
                var customNode = JsonNode.Parse(customSamplePayloadJson)?.AsObject();
                var defaultNode = JsonNode.Parse(defaultPayload)?.AsObject();

                if (customNode != null && defaultNode != null)
                {
                    foreach (var kvp in defaultNode)
                    {
                        if (!customNode.ContainsKey(kvp.Key))
                        {
                            customNode[kvp.Key] = kvp.Value?.DeepClone();
                        }
                    }
                    finalPayload = customNode.ToJsonString(JsonOptions);
                }
            }
            catch
            {
                // Fallback to default payload on parse failure
            }
        }

        string schema = InferSchemaInternal(allTokens, explicitArrayKeys, finalPayload, templateSlug);
        return (schema, finalPayload);
    }

    private static string InferSchemaInternal(
        IEnumerable<string> placeholders,
        ISet<string>? explicitArrayKeys,
        string? samplePayloadJson,
        string? templateSlug)
    {
        var rootProperties = new JsonObject();
        var requiredFields = new JsonArray();

        var arrayGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var objectGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var scalarPlaceholders = new List<string>();

        // 1. Classify tokens into scalar, single nested object, or array collection
        foreach (var ph in placeholders)
        {
            if (string.IsNullOrWhiteSpace(ph)) continue;
            var parsed = PlaceholderHelper.Parse(ph);

            if (parsed.Key.Contains('.'))
            {
                var parts = parsed.Key.Split('.', 2);
                string groupKey = parts[0].Trim();
                string fieldKey = parts[1].Trim();

                string itemPh = string.IsNullOrEmpty(parsed.Extra) ? fieldKey : $"{fieldKey}:{parsed.Extra}";

                bool isArray = explicitArrayKeys != null
                    ? explicitArrayKeys.Contains(groupKey)
                    : IsCollectionName(groupKey);

                if (isArray)
                {
                    if (!arrayGroups.ContainsKey(groupKey))
                        arrayGroups[groupKey] = new List<string>();
                    arrayGroups[groupKey].Add(itemPh);
                }
                else
                {
                    if (!objectGroups.ContainsKey(groupKey))
                        objectGroups[groupKey] = new List<string>();
                    objectGroups[groupKey].Add(itemPh);
                }
            }
            else
            {
                scalarPlaceholders.Add(ph);
            }
        }

        // 2. Add scalar properties
        foreach (var ph in scalarPlaceholders)
        {
            var parsed = PlaceholderHelper.Parse(ph);
            string key = parsed.Key;
            if (rootProperties.ContainsKey(key)) continue;

            var propObj = InferPropertySchema(key, parsed.Extra, parsed.Kind);
            rootProperties[key] = propObj;
            requiredFields.Add(key);
        }

        // 3. Add single nested object properties (e.g. customer.name, customer.tax_id)
        foreach (var (objKey, subFields) in objectGroups)
        {
            var itemProperties = new JsonObject();
            var itemRequired = new JsonArray();

            foreach (var subPh in subFields)
            {
                var parsed = PlaceholderHelper.Parse(subPh);
                if (itemProperties.ContainsKey(parsed.Key)) continue;

                itemProperties[parsed.Key] = InferPropertySchema(parsed.Key, parsed.Extra, parsed.Kind);
                itemRequired.Add(parsed.Key);
            }

            var objectProp = new JsonObject
            {
                ["type"] = "object",
                ["description"] = $"Information for {objKey}",
                ["required"] = itemRequired,
                ["properties"] = itemProperties,
                ["additionalProperties"] = false
            };

            rootProperties[objKey] = objectProp;
            requiredFields.Add(objKey);
        }

        // 4. Add array properties (e.g. items)
        foreach (var (arrayKey, itemPlaceholders) in arrayGroups)
        {
            var itemProperties = new JsonObject();
            var itemRequired = new JsonArray();

            foreach (var itemPh in itemPlaceholders)
            {
                var parsed = PlaceholderHelper.Parse(itemPh);
                if (itemProperties.ContainsKey(parsed.Key)) continue;

                itemProperties[parsed.Key] = InferPropertySchema(parsed.Key, parsed.Extra, parsed.Kind);
                itemRequired.Add(parsed.Key);
            }

            var arrayProp = new JsonObject
            {
                ["type"] = "array",
                ["description"] = $"List of {arrayKey} entries",
                ["minItems"] = 1,
                ["items"] = new JsonObject
                {
                    ["type"] = "object",
                    ["required"] = itemRequired,
                    ["properties"] = itemProperties,
                    ["additionalProperties"] = false
                }
            };

            rootProperties[arrayKey] = arrayProp;
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

    private static string GenerateDefaultSamplePayloadInternal(IEnumerable<string> placeholders, ISet<string>? explicitArrayKeys)
    {
        var root = new JsonObject();
        var arrayGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var objectGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var ph in placeholders)
        {
            if (string.IsNullOrWhiteSpace(ph)) continue;
            var parsed = PlaceholderHelper.Parse(ph);

            if (parsed.Key.Contains('.'))
            {
                var parts = parsed.Key.Split('.', 2);
                string groupKey = parts[0].Trim();
                string fieldKey = parts[1].Trim();

                string itemPh = string.IsNullOrEmpty(parsed.Extra) ? fieldKey : $"{fieldKey}:{parsed.Extra}";

                bool isArray = explicitArrayKeys != null
                    ? explicitArrayKeys.Contains(groupKey)
                    : IsCollectionName(groupKey);

                if (isArray)
                {
                    if (!arrayGroups.ContainsKey(groupKey))
                        arrayGroups[groupKey] = new List<string>();
                    arrayGroups[groupKey].Add(itemPh);
                }
                else
                {
                    if (!objectGroups.ContainsKey(groupKey))
                        objectGroups[groupKey] = new List<string>();
                    objectGroups[groupKey].Add(itemPh);
                }
            }
            else
            {
                if (!root.ContainsKey(parsed.Key))
                {
                    root[parsed.Key] = GenerateMockValue(parsed.Key, parsed.Extra, parsed.Kind);
                }
            }
        }

        // Generate nested objects
        foreach (var (objKey, subFields) in objectGroups)
        {
            var nestedObj = new JsonObject();
            foreach (var subPh in subFields)
            {
                var parsed = PlaceholderHelper.Parse(subPh);
                if (!nestedObj.ContainsKey(parsed.Key))
                {
                    nestedObj[parsed.Key] = GenerateMockValue(parsed.Key, parsed.Extra, parsed.Kind);
                }
            }
            root[objKey] = nestedObj;
        }

        // Generate array of objects
        foreach (var (arrayKey, itemPlaceholders) in arrayGroups)
        {
            var arr = new JsonArray();

            for (int i = 1; i <= 2; i++)
            {
                var itemObj = new JsonObject();
                foreach (var itemPh in itemPlaceholders)
                {
                    var parsed = PlaceholderHelper.Parse(itemPh);
                    if (parsed.Key.Equals("no", StringComparison.OrdinalIgnoreCase) || parsed.Key.Equals("seq", StringComparison.OrdinalIgnoreCase))
                    {
                        itemObj[parsed.Key] = i.ToString();
                    }
                    else if (IsIntegerKey(parsed.Key, parsed.Extra))
                    {
                        itemObj[parsed.Key] = i;
                    }
                    else if (IsNumericKey(parsed.Key, parsed.Extra))
                    {
                        itemObj[parsed.Key] = (decimal)(i * 1500);
                    }
                    else
                    {
                        itemObj[parsed.Key] = $"ตัวอย่าง {parsed.Key} {i}";
                    }
                }
                arr.Add(itemObj);
            }

            root[arrayKey] = arr;
        }

        return root.ToJsonString(JsonOptions);
    }

    private static JsonObject InferPropertySchema(string key, string? extra, PlaceholderHelper.PlaceholderKind kind)
    {
        var obj = new JsonObject();
        string lk = key.ToLowerInvariant();

        if (kind == PlaceholderHelper.PlaceholderKind.Qr)
        {
            obj["type"] = "string";
            obj["format"] = "uri";
            obj["description"] = "QR Code target URL or URI";
            return obj;
        }

        if (kind == PlaceholderHelper.PlaceholderKind.Barcode)
        {
            obj["type"] = "string";
            obj["pattern"] = "^[A-Za-z0-9-_]+$";
            obj["description"] = "Barcode alphanumeric code string";
            return obj;
        }

        if (kind == PlaceholderHelper.PlaceholderKind.Image)
        {
            obj["type"] = "string";
            obj["description"] = "Image URL or Base64 data URI";
            return obj;
        }

        // Thai Tax ID / Citizen ID (13 digits)
        if (lk == "tax_id" || lk == "taxid" || lk == "citizen_id" || lk == "citizenid" ||
            lk == "idcard" || lk == "id_card" || lk == "identification_no" || lk == "personal_id")
        {
            obj["type"] = "string";
            obj["pattern"] = "^[0-9]{13}$";
            obj["description"] = "13-digit Thai tax or citizen identification number";
            return obj;
        }

        // Email
        if (lk.Contains("email") || lk.Contains("e_mail"))
        {
            obj["type"] = "string";
            obj["format"] = "email";
            obj["description"] = "Valid email address";
            return obj;
        }

        // Phone / Tel
        if (lk.Contains("tel") || lk.Contains("phone") || lk.Contains("mobile") || lk.Contains("fax"))
        {
            obj["type"] = "string";
            obj["pattern"] = @"^[0-9+()\-\s]{9,20}$";
            obj["description"] = "Contact telephone or mobile number";
            return obj;
        }

        // Date
        if (IsDateKey(key, extra))
        {
            obj["type"] = "string";
            obj["format"] = "date";
            obj["description"] = $"Date value for {key} (YYYY-MM-DD)";
            return obj;
        }

        // Time
        if (lk.Contains("time") && !lk.Contains("date"))
        {
            obj["type"] = "string";
            obj["format"] = "time";
            obj["description"] = $"Time value for {key} (HH:mm:ss)";
            return obj;
        }

        // Integer / Quantity / Sequence
        if (IsIntegerKey(key, extra))
        {
            obj["type"] = "integer";
            obj["minimum"] = (lk == "qty" || lk == "quantity" || lk == "no" || lk == "seq" || lk == "item_no") ? 1 : 0;
            obj["description"] = $"Integer quantity or sequence for {key}";
            return obj;
        }

        // Numeric / Financial
        if (IsNumericKey(key, extra))
        {
            obj["type"] = "number";
            obj["minimum"] = 0;
            obj["description"] = $"Numeric amount or price for {key}";
            return obj;
        }

        // Default string with minLength: 1
        obj["type"] = "string";
        obj["minLength"] = 1;
        obj["description"] = $"Text value for {key}";
        return obj;
    }

    private static JsonNode GenerateMockValue(string key, string? extra, PlaceholderHelper.PlaceholderKind kind)
    {
        if (kind == PlaceholderHelper.PlaceholderKind.Qr)
            return JsonValue.Create("https://sammakorn.co.th");
        if (kind == PlaceholderHelper.PlaceholderKind.Barcode)
            return JsonValue.Create("SMK-998822");
        if (kind == PlaceholderHelper.PlaceholderKind.Image)
            return JsonValue.Create("https://placehold.co/300x200/png");

        string lk = key.ToLowerInvariant();

        if (lk == "tax_id" || lk == "taxid" || lk == "citizen_id" || lk == "citizenid" ||
            lk == "idcard" || lk == "id_card" || lk == "identification_no" || lk == "personal_id")
        {
            return JsonValue.Create("0107536000123");
        }

        if (IsIntegerKey(key, extra))
            return JsonValue.Create(1);

        if (IsNumericKey(key, extra))
            return JsonValue.Create(250000.00m);

        if (IsDateKey(key, extra))
            return JsonValue.Create(DateTime.UtcNow.ToString("yyyy-MM-dd"));

        if (lk.Contains("email"))
            return JsonValue.Create("contact@sammakorn.co.th");

        if (lk.Contains("phone") || lk.Contains("tel"))
            return JsonValue.Create("02-123-4567");

        if (lk.Contains("doc_no") || lk.Contains("inv_no") || lk.Contains("contract_no") || lk.Contains("receipt_no"))
            return JsonValue.Create($"INV-{DateTime.UtcNow:yyyy}-0001");

        if (lk.Contains("address"))
            return JsonValue.Create("123/45 ถนนพัฒนาการ แขวงสวนหลวง กรุงเทพมหานคร 10250");

        if (lk.Contains("name"))
            return JsonValue.Create("บริษัท สัมมากร จำกัด (มหาชน)");

        return JsonValue.Create($"ตัวอย่าง {key}");
    }

    private static bool IsIntegerKey(string key, string? extra)
    {
        if (!string.IsNullOrEmpty(extra))
        {
            string ext = extra.ToLowerInvariant();
            if (ext is "int" or "integer" or "count") return true;
        }

        string lk = key.ToLowerInvariant();
        return lk == "qty" || lk == "quantity" || lk == "count" || lk == "seq" || lk == "no" || lk == "item_no" || lk == "unit" || lk == "age";
    }

    private static bool IsNumericKey(string key, string? extra)
    {
        if (IsIntegerKey(key, extra)) return false;

        if (!string.IsNullOrEmpty(extra))
        {
            string ext = extra.ToLowerInvariant();
            if (ext is "number" or "currency" or "currency0" or "baht" or "thaibaht" or "thai_baht_text" or "baht_text" or "bahttext" or "decimal")
                return true;
        }

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
            if (ext.Contains("date") || ext.Contains("time"))
                return true;
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

        if (lk is "customer" or "buyer" or "seller" or "vendor" or "company" or "recipient" or "issuer" or "payer" or "payee" or "approver" or "applicant" or "tenant" or "landlord" or "owner" or "client" or "header" or "footer" or "meta" or "config" or "contact" or "sender" or "receiver" or "user" or "profile")
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
}
