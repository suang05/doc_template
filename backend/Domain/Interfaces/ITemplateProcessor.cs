using SmkDocServer.Domain.Models;

namespace SmkDocServer.Domain.Interfaces;

/// <summary>
/// Routes a template document through its corresponding rendering engine
/// and returns processed document bytes.
/// Implementations: DocxTemplateProcessor, HtmlTemplateProcessor (Sprint 5), ReportBroTemplateProcessor (Sprint 8).
/// </summary>
public interface ITemplateProcessor
{
    /// <summary>The engine type this processor handles.</summary>
    TemplateEngineType EngineType { get; }

    /// <summary>
    /// Processes the template with the provided context and returns output bytes
    /// (PDF, DOCX, or XLSX depending on context.OutputFormat).
    /// </summary>
    Task<byte[]> ProcessAsync(TemplateProcessingContext ctx, CancellationToken cancellationToken = default);
}
