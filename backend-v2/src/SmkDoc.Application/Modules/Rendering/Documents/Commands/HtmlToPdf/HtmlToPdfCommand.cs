namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.HtmlToPdf;

/// <summary>
/// Command for converting raw HTML directly to PDF bytes.
/// </summary>
public record HtmlToPdfCommand(
    string Html,
    string? HeaderHtml = null,
    string? FooterHtml = null
);
