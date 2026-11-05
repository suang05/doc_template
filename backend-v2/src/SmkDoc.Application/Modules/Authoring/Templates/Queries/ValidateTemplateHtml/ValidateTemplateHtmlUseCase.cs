using System.Text;
using System.Text.RegularExpressions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplateHtml;

/// <summary>
/// Validates HTML template syntax, extracts Handlebars and &lt;Field&gt; placeholders, and performs a Gotenberg dry-run render.
/// </summary>
public sealed partial class ValidateTemplateHtmlUseCase(
    IPdfRenderer pdfRenderer,
    ITemplateScannerService scanner) : IUseCase<ValidateTemplateHtmlQuery, TemplateValidationResultDto>
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

        var (fields, errors) = await ExtractFieldsAsync(query.Html, ct);
        await ValidateRenderAsync(query.Html, errors, ct);

        bool isValid = errors.Count == 0;
        return new TemplateValidationResultDto(isValid, fields, errors);
    }

    private async Task<(List<string> Fields, List<string> Errors)> ExtractFieldsAsync(string html, CancellationToken ct)
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        // 1. Scan Handlebars {{placeholders}} using SSoT TemplateScannerService
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(html));
        var scannedPlaceholders = await scanner.ScanPlaceholdersAsync(stream, ".html", ct);
        foreach (var placeholder in scannedPlaceholders)
        {
            fields.Add(placeholder);
        }

        // 2. Scan legacy <Field name="..."> tags for backward-compatibility
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
            fields.Add(fieldName);
        }

        return (fields.ToList(), errors);
    }

    private async Task ValidateRenderAsync(string html, List<string> errors, CancellationToken ct)
    {
        try
        {
            await using var pdfStream = await pdfRenderer.RenderHtmlToPdfStreamAsync(html, ct: ct);
            if (pdfStream is not null)
            {
                await pdfStream.CopyToAsync(Stream.Null, 81_920, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors.Add($"Renderer validation error: {ex.Message}");
        }
    }
}
