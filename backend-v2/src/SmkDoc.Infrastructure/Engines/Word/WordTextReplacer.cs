using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SmkDoc.Application.Common.Helpers;

namespace SmkDoc.Infrastructure.Engines.Word;

public class WordTextReplacer
{
    public void ReplaceTexts(MainDocumentPart mainPart, Dictionary<string, string> replacements)
    {
        if (replacements.Count == 0) return;

        // Process all paragraphs in body
        var paragraphs = mainPart.Document.Body?.Descendants<Paragraph>() ?? Enumerable.Empty<Paragraph>();
        foreach (var p in paragraphs)
        {
            ReplaceInParagraph(p, replacements);
        }

        // Process headers and footers
        foreach (var header in mainPart.HeaderParts)
        {
            foreach (var p in header.Header.Descendants<Paragraph>())
            {
                ReplaceInParagraph(p, replacements);
            }
        }

        foreach (var footer in mainPart.FooterParts)
        {
            foreach (var p in footer.Footer.Descendants<Paragraph>())
            {
                ReplaceInParagraph(p, replacements);
            }
        }
    }

    private void ReplaceInParagraph(Paragraph p, Dictionary<string, string> replacements)
    {
        string text = p.InnerText;
        if (!text.Contains("{{")) return;

        var runs = p.Elements<Run>().ToList();
        if (runs.Count == 0) return;

        string combined = string.Concat(runs.Select(r => r.InnerText));
        string updated = PlaceholderHelper.Pattern.Replace(combined, m =>
        {
            string key = m.Groups[1].Value.Trim();
            if (replacements.TryGetValue(key, out var val))
                return val;

            var parsed = PlaceholderHelper.Parse(key);
            if (parsed.Kind == PlaceholderHelper.PlaceholderKind.Transform &&
                replacements.TryGetValue(parsed.Key, out var rawVal))
            {
                return ThaiDataTransformer.Transform(rawVal, parsed.Extra ?? "");
            }

            return m.Value;
        });

        if (updated != combined)
        {
            var firstRunProps = runs[0].RunProperties?.CloneNode(true) as RunProperties;
            p.RemoveAllChildren<Run>();

            var newRun = new Run();
            if (firstRunProps != null) newRun.AppendChild(firstRunProps);
            newRun.AppendChild(new Text(updated));
            p.AppendChild(newRun);
        }
    }
}
