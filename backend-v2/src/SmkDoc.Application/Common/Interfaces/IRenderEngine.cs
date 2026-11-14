using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Port for document template render engines (HTML, Word, Excel).
/// Exposes pure Zero-LOH stream contracts only (no byte-array buffering).
/// </summary>
public interface IRenderEngine
{
    /// <summary>
    /// The template format / engine type handled by this implementation.
    /// </summary>
    RenderEngineType EngineType { get; }

    /// <summary>
    /// Renders the template with provided data directly into an output stream (Zero-LOH Buffering).
    /// </summary>
    Task<Stream> RenderStreamAsync(Stream templateStream, string inputDataJson, OutputFormat outputFormat, CancellationToken ct);
}
