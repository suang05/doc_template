using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmkDocServer.Domain.Interfaces;
using SmkDocServer.Infrastructure.Data;

namespace SmkDocServer.Application.Services;

/// <summary>
/// Inspects a template file and extracts its placeholder schema (variable names, types, table columns).
/// Used by the Field Mapping UI to show users what variables are available.
/// SRP: This class is responsible ONLY for schema/variable introspection — not storage, not versioning.
/// </summary>
public class TemplateSchemaService : ITemplateSchemaService
{
    private readonly AppDbContext _dbContext;
    private readonly ITemplateResolverService _resolver;
    private readonly ILogger<TemplateSchemaService> _logger;

    public TemplateSchemaService(
        AppDbContext dbContext,
        ITemplateResolverService resolver,
        ILogger<TemplateSchemaService> logger)
    {
        _dbContext = dbContext;
        _resolver = resolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<object> InspectSchemaAsync(string fileName, Guid? projectId = null)
    {
        string? path = await _resolver.ResolveTemplatePathAsync(fileName, projectId);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            throw new FileNotFoundException($"Template '{fileName}' not found.");

        string ext = Path.GetExtension(path).ToLowerInvariant();
        var variables = new Dictionary<string, (string Type, List<string>? Columns)>();

        try
        {
            using var zip = System.IO.Compression.ZipFile.OpenRead(path);
            var textContent = new System.Text.StringBuilder();

            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream);
                    textContent.Append(reader.ReadToEnd());
                    textContent.Append(' ');
                }
            }

            string rawXml = textContent.ToString();
            // Strip XML tags so variables split across runs are cleaned up
            string raw = System.Text.RegularExpressions.Regex.Replace(rawXml, @"<[^>]+>", "");

            // Match loops: {{#Items}} ... {{/Items}}
            var loopMatches = System.Text.RegularExpressions.Regex.Matches(raw,
                @"\{\{#([a-zA-Z0-9_]+)\}\}(.*?)\{\{/\1\}\}",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            var tableVarNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (System.Text.RegularExpressions.Match loop in loopMatches)
            {
                string loopName = loop.Groups[1].Value;
                string inner = loop.Groups[2].Value;
                tableVarNames.Add(loopName);

                var colMatches = System.Text.RegularExpressions.Regex.Matches(inner, @"\{\{([a-zA-Z0-9_]+)\}\}");
                var cols = new List<string>();
                foreach (System.Text.RegularExpressions.Match col in colMatches)
                {
                    string colName = col.Groups[1].Value;
                    if (!cols.Contains(colName) && !colName.StartsWith('#') && !colName.StartsWith('/'))
                        cols.Add(colName);
                }
                variables[loopName] = ("table", cols);
            }

            // Match normal variables: {{variable}}
            var allVarMatches = System.Text.RegularExpressions.Regex.Matches(raw, @"\{\{([^}]+)\}\}");
            foreach (System.Text.RegularExpressions.Match m in allVarMatches)
            {
                string token = m.Groups[1].Value.Trim();
                if (token.StartsWith('#') || token.StartsWith('/') || token.StartsWith('$')) continue;

                if (token.StartsWith("@qr:", StringComparison.OrdinalIgnoreCase))
                    variables[token.Substring(4).Trim()] = ("qrcode", null);
                else if (token.StartsWith("qr:", StringComparison.OrdinalIgnoreCase))
                    variables[token.Substring(3).Trim()] = ("qrcode", null);
                else if (token.StartsWith("qrcode:", StringComparison.OrdinalIgnoreCase))
                    variables[token.Substring(7).Trim()] = ("qrcode", null);
                else if (token.StartsWith("@bc:", StringComparison.OrdinalIgnoreCase))
                    variables[token.Substring(4).Trim()] = ("barcode", null);
                else if (token.StartsWith("barcode:", StringComparison.OrdinalIgnoreCase))
                    variables[token.Substring(8).Trim()] = ("barcode", null);
                else if (!tableVarNames.Contains(token) && !variables.ContainsKey(token))
                {
                    string type = "text";
                    if (token.IndexOf("date", StringComparison.OrdinalIgnoreCase) >= 0) type = "date";
                    else if (token.IndexOf("price", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             token.IndexOf("total", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             token.IndexOf("amount", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             token.IndexOf("vat", StringComparison.OrdinalIgnoreCase) >= 0) type = "number";
                    variables[token] = (type, null);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not inspect schema for '{FileName}'", fileName);
        }

        var varList = variables.Select(kv => new
        {
            Key = kv.Key,
            Type = kv.Value.Type,
            Label = kv.Key,
            Columns = kv.Value.Columns
        }).ToList();

        var warnings = new List<string>();
        if (ext == ".xlsx")
        {
            foreach (var kv in variables)
            {
                if (kv.Key.Contains('.') && !kv.Key.StartsWith("row:", StringComparison.OrdinalIgnoreCase))
                    warnings.Add($"ตัวแปร '{{{{{kv.Key}}}}}' ใน Excel ควรเปลี่ยนเป็น '{{{{row:{kv.Key.Split('.').Last()}}}}}' เพื่อให้ ClosedXML แตกแถวตารางอัตโนมัติ");
            }
        }
        if (varList.Count == 0)
            warnings.Add("ไม่พบตัวแปร {{...}} ในไฟล์แม่แบบ กรุณาตรวจสอบว่าใส่เครื่องหมายปีกกาคู่ถูกต้อง");

        // Generate sample data for quick testing
        var sampleData = new Dictionary<string, object?>();
        foreach (var kv in variables)
        {
            string key = kv.Key;
            string type = kv.Value.Type;
            if (type == "table")
            {
                var cols = kv.Value.Columns ?? new List<string> { "ItemNo", "Description", "Quantity", "UnitPrice", "Amount" };
                var row1 = new Dictionary<string, object?>();
                var row2 = new Dictionary<string, object?>();
                int idx = 1;
                foreach (var col in cols)
                {
                    if (col.IndexOf("no", StringComparison.OrdinalIgnoreCase) >= 0 || col.IndexOf("item", StringComparison.OrdinalIgnoreCase) >= 0)
                    { row1[col] = "1"; row2[col] = "2"; }
                    else if (col.IndexOf("price", StringComparison.OrdinalIgnoreCase) >= 0 || col.IndexOf("amount", StringComparison.OrdinalIgnoreCase) >= 0 || col.IndexOf("total", StringComparison.OrdinalIgnoreCase) >= 0)
                    { row1[col] = 1500; row2[col] = 3000; }
                    else if (col.IndexOf("qty", StringComparison.OrdinalIgnoreCase) >= 0 || col.IndexOf("quantity", StringComparison.OrdinalIgnoreCase) >= 0)
                    { row1[col] = 1; row2[col] = 2; }
                    else { row1[col] = $"รายการที่ {idx++}"; }
                }
                sampleData[key] = new List<Dictionary<string, object?>> { row1, row2 };
            }
            else if (type == "number") sampleData[key] = 25000;
            else if (type == "date") sampleData[key] = DateTime.UtcNow.ToString("yyyy-MM-dd");
            else if (type == "qrcode") sampleData[key] = "https://www.sammakorn.co.th";
            else if (type == "barcode") sampleData[key] = "SMK-2026-0001";
            else
            {
                if (key.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0) sampleData[key] = "คุณสมชาย สัมมากร";
                else if (key.IndexOf("project", StringComparison.OrdinalIgnoreCase) >= 0) sampleData[key] = "สัมมากร รามคำแหง";
                else if (key.IndexOf("doc", StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf("no", StringComparison.OrdinalIgnoreCase) >= 0) sampleData[key] = "SMK-DOC-2026-001";
                else if (key.IndexOf("address", StringComparison.OrdinalIgnoreCase) >= 0) sampleData[key] = "123/45 ถนนรามคำแหง แขวงสะพานสูง เขตสะพานสูง กทม. 10240";
                else sampleData[key] = $"ข้อมูลทดสอบ ({key})";
            }
        }

        // Retrieve version info
        int currentVersion = 1;
        int versionsCount = 1;
        try
        {
            var meta = await _dbContext.Templates
                .Include(t => t.Versions)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.FileName == fileName && (t.IsGlobal || t.ProjectId == projectId));
            if (meta != null)
            {
                currentVersion = meta.CurrentVersion;
                versionsCount = meta.Versions != null && meta.Versions.Count > 0 ? meta.Versions.Count : 1;
            }
        }
        catch { }

        return new
        {
            FileName = fileName,
            Format = ext.TrimStart('.'),
            Variables = varList,
            Count = varList.Count,
            CurrentVersion = currentVersion,
            VersionsCount = versionsCount,
            Warnings = warnings,
            SampleData = sampleData
        };
    }
}
