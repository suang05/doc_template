using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SmkDoc.Application.Modules.Authoring.Templates.Parsing;

namespace SmkDoc.Infrastructure.Engines.Word;

public static class WordTableExpander
{
    public static void ExpandTables(MainDocumentPart mainPart, IReadOnlyList<IReadOnlyList<Dictionary<string, string>>> tableDataList)
    {
        if (tableDataList.Count == 0) return;

        var tables = mainPart.Document.Body?.Descendants<Table>().ToList() ?? new List<Table>();
        for (int i = 0; i < tables.Count && i < tableDataList.Count; i++)
        {
            var table = tables[i];
            var rowsData = tableDataList[i];
            ExpandSingleTable(table, rowsData);
        }
    }

    private static void ExpandSingleTable(Table table, IReadOnlyList<Dictionary<string, string>> rowsData)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count < 2 || rowsData.Count == 0) return;

        // Use last row or row containing {{...}} as template row
        var templateRow = rows.LastOrDefault(r => r.InnerText.Contains("{{", StringComparison.Ordinal));
        if (templateRow == null) return;

        foreach (var rowData in rowsData)
        {
            if (templateRow.CloneNode(true) is not TableRow clonedRow) continue;

            PopulateClonedRow(clonedRow, rowData);
            table.InsertBefore(clonedRow, templateRow);
        }

        // Remove the original template row
        templateRow.Remove();
    }

    private static void PopulateClonedRow(TableRow clonedRow, Dictionary<string, string> rowData)
    {
        foreach (var cell in clonedRow.Elements<TableCell>())
        {
            foreach (var paragraph in cell.Elements<Paragraph>())
            {
                ReplaceParagraphPlaceholders(paragraph, rowData);
            }
        }
    }

    private static void ReplaceParagraphPlaceholders(Paragraph paragraph, Dictionary<string, string> rowData)
    {
        if (!paragraph.InnerText.Contains("{{", StringComparison.Ordinal)) return;

        var runs = paragraph.Elements<Run>().ToList();
        string combined = string.Concat(runs.Select(r => r.InnerText));
        string updated = TemplatePlaceholderParser.Pattern.Replace(combined, m =>
        {
            string key = m.Groups[1].Value.Trim();
            return rowData.TryGetValue(key, out var val) ? val : m.Value;
        });

        if (updated == combined) return;

        var firstProps = runs.FirstOrDefault()?.RunProperties?.CloneNode(true) as RunProperties;
        paragraph.RemoveAllChildren<Run>();

        var newRun = new Run();
        if (firstProps != null) newRun.AppendChild(firstProps);
        newRun.AppendChild(new Text(updated));
        paragraph.AppendChild(newRun);
    }
}
