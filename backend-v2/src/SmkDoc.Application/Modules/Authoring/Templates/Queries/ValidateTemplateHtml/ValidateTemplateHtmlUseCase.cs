using System.Text.RegularExpressions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplateHtml;

/// <summary>
/// Validates HTML template syntax, detects <Field> tags, and performs a Gotenberg dry-run render.
/// </summary>
public partial class ValidateTemplateHtmlUseCase(IPdfRenderer pdfRenderer)
{
    [GeneratedRegex(@"<Field\s+([^>]+?)\s*\/?>", RegexOptions.IgnoreCase)]
    private static partial Regex FieldTagRegex();

    [GeneratedRegex(@"name=[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex NameAttributeRegex();

    [GeneratedRegex(@"label=[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex LabelAttributeRegex();

    public async Task<TemplateValidationResultDto> ValidateHtmlAsync(string html, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return new TemplateValidationResultDto(
                Valid: false,
                Fields: [],
                Errors: ["HTML template cannot be empty."]
            );
        }

        var (fields, errors) = ExtractFields(html);
        await ValidateRenderAsync(html, errors, ct);

        bool isValid = errors.Count == 0 || !errors.Any(e => e.StartsWith("Renderer validation error:"));
        return new TemplateValidationResultDto(isValid, fields, errors);
    }

    private static (List<string> Fields, List<string> Errors) ExtractFields(string html)
    {
        var fields = new List<string>();
        var errors = new List<string>();

        foreach (Match match in FieldTagRegex().Matches(html))
        {
            string attributes = match.Groups[1].Value;
            var nameMatch = NameAttributeRegex().Match(attributes);
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

            if (!LabelAttributeRegex().IsMatch(attributes))
            {
                errors.Add($"<Field name=\"{fieldName}\"> is missing recommended 'label' attribute.");
            }
        }

        return (fields, errors);
    }

    private async Task ValidateRenderAsync(string html, List<string> errors, CancellationToken ct)
    {
        try
        {
            await pdfRenderer.RenderHtmlToPdfAsync(html, ct: ct);
        }
        catch (Exception ex)
        {
            errors.Add($"Renderer validation error: {ex.Message}");
        }
    }
}
