using SmkDocServer.Domain.Interfaces;
using SmkDocServer.Domain.Models;
using SmkDocServer.Infrastructure.Helpers;

namespace SmkDocServer.Infrastructure.Services.Document;

/// <summary>
/// Renders Tiptap HTML templates: regex-replaces {{var}} tokens then converts via Gotenberg Chromium.
/// Registered as ITemplateProcessor for EngineType.Tiptap (Sprint 5).
/// </summary>
public class HtmlTemplateProcessor : ITemplateProcessor
{
    private readonly IPdfConverter _pdfConverter;

    public HtmlTemplateProcessor(IPdfConverter pdfConverter)
    {
        _pdfConverter = pdfConverter;
    }

    public TemplateEngineType EngineType => TemplateEngineType.Tiptap;

    public async Task<byte[]> ProcessAsync(TemplateProcessingContext ctx, CancellationToken cancellationToken = default)
    {
        string html = await File.ReadAllTextAsync(ctx.TemplatePath, System.Text.Encoding.UTF8, cancellationToken);

        // Replace {{var}} using PlaceholderHelper SSoT (Rule 11 — DRY)
        html = PlaceholderHelper.Pattern.Replace(html, match =>
        {
            string key = match.Groups[1].Value;
            return ctx.Data.Replace.TryGetValue(key, out string? val) && val != null
                ? System.Net.WebUtility.HtmlEncode(val)
                : match.Value;
        });

        return await _pdfConverter.ConvertHtmlToPdfAsync(html);
    }
}
