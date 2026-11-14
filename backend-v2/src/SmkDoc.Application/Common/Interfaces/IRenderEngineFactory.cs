using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Polymorphic factory port providing deterministic O(1) resolution of document rendering engines.
/// Adheres strictly to Invariant #3 (ExtensibleEngines).
/// </summary>
public interface IRenderEngineFactory
{
    /// <summary>
    /// Resolves the specialized render engine for the specified engine type.
    /// </summary>
    /// <param name="engineType">The target render engine type (Html, Docx, Excel).</param>
    /// <returns>The matching <see cref="IRenderEngine"/> implementation.</returns>
    /// <exception cref="RenderException">
    /// Thrown when no engine is registered for the given engine type.
    /// </exception>
    IRenderEngine GetEngine(RenderEngineType engineType);
}

