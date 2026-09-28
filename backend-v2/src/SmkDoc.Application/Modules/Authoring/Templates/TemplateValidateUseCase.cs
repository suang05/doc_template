using System.Text.RegularExpressions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Templates;

public sealed class TemplateValidateUseCase(IPdfRenderer pdfRenderer)
{
    private static readonly Regex FieldTagPattern = new(@"<Field\s+([^>]+?)\s*\/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NameAttributePattern = new(@"name=[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LabelAttributePattern = new(@"label=[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<TemplateValidationResultDto> ValidateHtmlAsync(string html, CancellationToken ct = default)
    {
        var fields = new List<string>();
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(html))
        {
            errors.Add("HTML template cannot be empty.");
            return new TemplateValidationResultDto(false, fields, errors);
        }

        // Scan <Field> tags
        var matches = FieldTagPattern.Matches(html);
        foreach (Match match in matches)
        {
            string attributes = match.Groups[1].Value;
            var nameMatch = NameAttributePattern.Match(attributes);
            if (!nameMatch.Success)
            {
                errors.Add($"Found <Field> tag missing 'name' attribute: '{match.Value}'");
                continue;
            }

            string fieldName = nameMatch.Groups[1].Value;
            if (!fields.Contains(fieldName))
            {
                fields.Add(fieldName);
            }

            var labelMatch = LabelAttributePattern.Match(attributes);
            if (!labelMatch.Success)
            {
                errors.Add($"<Field name=\"{fieldName}\"> is missing recommended 'label' attribute.");
            }
        }

        // Dry-run render via Gotenberg Chromium
        try
        {
            await pdfRenderer.RenderHtmlToPdfAsync(html, ct: ct);
        }
        catch (Exception ex)
        {
            errors.Add($"Renderer validation error: {ex.Message}");
        }

        bool isValid = errors.Count == 0 || !errors.Any(e => e.StartsWith("Renderer validation error:"));
        return new TemplateValidationResultDto(isValid, fields, errors);
    }
}
