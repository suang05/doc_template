using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SmkDoc.Application.Common.Helpers;

namespace SmkDoc.Infrastructure.Engines.Word;

public class WordTableExpander
{

    public void ExpandTables(MainDocumentPart mainPart, List<List<Dictionary<string, string>>> tableDataList)
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

    private void ExpandSingleTable(Table table, List<Dictionary<string, string>> rowsData)
    {
        var rows = table.Elements<TableRow>().ToList();
        if (rows.Count < 2 || rowsData.Count == 0) return;

        // Use last row or row containing {{...}} as template row
        var templateRow = rows.LastOrDefault(r => r.InnerText.Contains("{{"));
        if (templateRow == null) return;

        foreach (var rowData in rowsData)
        {
            var clonedRow = templateRow.CloneNode(true) as TableRow;
            if (clonedRow == null) continue;

            foreach (var cell in clonedRow.Elements<TableCell>())
            {
                foreach (var p in cell.Elements<Paragraph>())
                {
                    string text = p.InnerText;
                    if (!text.Contains("{{")) continue;

                    var runs = p.Elements<Run>().ToList();
                    string combined = string.Concat(runs.Select(r => r.InnerText));
                    string updated = PlaceholderHelper.Pattern.Replace(combined, m =>
                    {
                        string key = m.Groups[1].Value.Trim();
                        return rowData.TryGetValue(key, out var val) ? val : m.Value;
                    });

                    if (updated != combined)
                    {
                        var firstProps = runs.FirstOrDefault()?.RunProperties?.CloneNode(true) as RunProperties;
                        p.RemoveAllChildren<Run>();

                        var newRun = new Run();
                        if (firstProps != null) newRun.AppendChild(firstProps);
                        newRun.AppendChild(new Text(updated));
                        p.AppendChild(newRun);
                    }
                }
            }

            table.InsertBefore(clonedRow, templateRow);
        }

        // Remove the original template row
        templateRow.Remove();
    }
}
