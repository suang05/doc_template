using ClosedXML.Excel;
using SmkDoc.Application.Common.Helpers;
using static SmkDoc.Application.Common.Helpers.PlaceholderHelper;

namespace SmkDoc.Infrastructure.Engines.Excel;

/// <summary>
/// Expands template rows that contain {{arrayKey.fieldName}} placeholders.
/// Convention: a row with {{items.name}}, {{items.qty}} is a template row for the "items" array.
/// Rows are duplicated per item, bottom-to-top to preserve row numbers.
/// </summary>
public class ExcelTableExpander
{

    public void ExpandTables(
        IXLWorksheet worksheet,
        Dictionary<string, List<Dictionary<string, string>>> arrays)
    {
        if (arrays.Count == 0) return;

        var templateRows = FindTemplateRows(worksheet, arrays);

        foreach (var (rowNum, arrayKey) in templateRows)
        {
            if (!arrays.TryGetValue(arrayKey, out var items)) continue;

            // Snapshot template cell content before any row operations
            var templateData = worksheet.Row(rowNum).CellsUsed()
                .Select(c => (Col: c.Address.ColumnNumber, Val: c.GetString()))
                .ToList();

            if (items.Count == 0)
            {
                worksheet.Row(rowNum).Delete();
                continue;
            }

            if (items.Count > 1)
            {
                // Insert n-1 blank rows below template row then copy formatting
                worksheet.Row(rowNum + 1).InsertRowsAbove(items.Count - 1);
                for (int i = 1; i < items.Count; i++)
                {
                    worksheet.Row(rowNum).CopyTo(worksheet.Cell(rowNum + i, 1));
                    worksheet.Row(rowNum + i).Height = worksheet.Row(rowNum).Height;
                }
            }

            for (int i = 0; i < items.Count; i++)
                FillRow(worksheet, rowNum + i, templateData, items[i], arrayKey);
        }
    }

    private static void FillRow(
        IXLWorksheet worksheet,
        int rowNum,
        List<(int Col, string Val)> templateData,
        Dictionary<string, string> item,
        string arrayKey)
    {
        foreach (var (col, templateVal) in templateData)
        {
            if (!templateVal.Contains("{{")) continue;

            var updated = Pattern.Replace(templateVal, m =>
            {
                var parsed = Parse(m.Groups[1].Value);

                string fullKey;
                string? transform = null;

                if (parsed.Kind == PlaceholderKind.Text)
                {
                    fullKey = parsed.Key;
                }
                else if (parsed.Kind == PlaceholderKind.Transform)
                {
                    fullKey = parsed.Key;
                    transform = parsed.Extra;
                }
                else
                {
                    return m.Value; // QR/Barcode handled by InjectMedia
                }

                // Only handle dot-notation: {{arrayKey.field}} or {{arrayKey.field:transform}}
                var dotIdx = fullKey.IndexOf('.');
                if (dotIdx <= 0) return m.Value; // Plain {{key}} — leave for text replacement

                var ak    = fullKey[..dotIdx];
                var field = fullKey[(dotIdx + 1)..];

                if (!string.Equals(ak, arrayKey, StringComparison.OrdinalIgnoreCase))
                    return m.Value;

                if (!item.TryGetValue(field, out var rawValue)) return m.Value;

                return transform is not null
                    ? ThaiDataTransformer.Transform(rawValue, transform)
                    : rawValue;
            });

            if (updated != templateVal)
                worksheet.Cell(rowNum, col).SetValue(updated);
        }
    }

    private static List<(int rowNum, string arrayKey)> FindTemplateRows(
        IXLWorksheet worksheet,
        Dictionary<string, List<Dictionary<string, string>>> arrays)
    {
        var result = new List<(int rowNum, string arrayKey)>();

        foreach (var row in worksheet.RowsUsed())
        {
            foreach (var cell in row.CellsUsed())
            {
                if (cell.HasFormula) continue;
                var cellText = cell.GetString();
                if (!cellText.Contains("{{")) continue;

                var found = false;
                foreach (System.Text.RegularExpressions.Match ph in Pattern.Matches(cellText))
                {
                    var inner = ph.Groups[1].Value.Trim();
                    // Strip any transform suffix before dot check
                    var keyPart = inner.Contains(':') ? inner[..inner.IndexOf(':')] : inner;
                    var dm = DotNotationPattern.Match(keyPart);
                    if (!dm.Success) continue;

                    var ak = dm.Groups[1].Value;
                    if (!arrays.ContainsKey(ak)) continue;

                    result.Add((row.RowNumber(), ak));
                    found = true;
                    break;
                }
                if (found) break;
            }
        }

        // Process bottom-to-top so row insertions don't shift higher template rows
        result.Sort((a, b) => b.rowNum.CompareTo(a.rowNum));
        return result;
    }
}
