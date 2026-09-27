namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when document rendering fails inside a render engine or PDF converter.
/// Maps to HTTP 500 Internal Server Error at the presentation layer.
/// </summary>
public sealed class RenderException : DomainException
{
    public string TemplateSlug { get; }
    public string EngineType { get; }

    public RenderException(string templateSlug, string engineType, string reason, Exception? innerException = null)
        : base($"Document rendering failed for template '{templateSlug}' using engine '{engineType}': {reason}",
               "DOCUMENT_RENDER_FAILED", 500, innerException)
    {
        TemplateSlug = templateSlug;
        EngineType = engineType;
    }
}
