using System.Text.RegularExpressions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplateHtml;

/// <summary>
/// Validates HTML template syntax, detects &lt;Field&gt; tags, and performs a Gotenberg dry-run render.
/// </summary>
public sealed partial class ValidateTemplateHtmlUseCase(IPdfRenderer pdfRenderer)
    : IUseCase<ValidateTemplateHtmlQuery, TemplateValidationResultDto>
{
    [GeneratedRegex(@"<Field\s+([^>]+?)\s*\/?>", RegexOptions.IgnoreCase)]
    private static partial Regex FieldTagRegex();

    [GeneratedRegex(@"name=[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex NameAttributeRegex();

    public async Task<TemplateValidationResultDto> ExecuteAsync(ValidateTemplateHtmlQuery query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.Html))
        {
            return new TemplateValidationResultDto(
                Valid: false,
                Fields: [],
                Errors: ["HTML template cannot be empty."]
            );
        }

        var (fields, errors) = ExtractFields(query.Html);
        await ValidateRenderAsync(query.Html, errors, ct);

        bool isValid = errors.Count == 0;
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
        }

        return (fields, errors);
    }

    private async Task ValidateRenderAsync(string html, List<string> errors, CancellationToken ct)
    {
        try
        {
            await pdfRenderer.RenderHtmlToPdfAsync(html, ct: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors.Add($"Renderer validation error: {ex.Message}");
        }
    }
}
