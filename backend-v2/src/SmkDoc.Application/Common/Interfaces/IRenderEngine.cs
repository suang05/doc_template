using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Port for document template render engines (HTML, Word, Excel).
/// Implemented in the Infrastructure layer by specialized engines.
/// </summary>
public interface IRenderEngine
{
    /// <summary>
    /// The template format / engine type handled by this implementation.
    /// </summary>
    RenderEngineType EngineType { get; }

    /// <summary>
    /// Renders the template with provided data into target output format as bytes.
    /// </summary>
    Task<byte[]> RenderAsync(Stream templateStream, string inputDataJson, OutputFormat outputFormat, CancellationToken ct = default);

    /// <summary>
    /// Renders the template with provided data directly into an output stream (Zero-LOH Buffering).
    /// </summary>
    Task<Stream> RenderStreamAsync(Stream templateStream, string inputDataJson, OutputFormat outputFormat, CancellationToken ct = default);
}
