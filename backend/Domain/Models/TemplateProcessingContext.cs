using SmkDocServer.Domain.Entities;
using SmkDocServer.Domain.Models;

namespace SmkDocServer.Domain.Models;

/// <summary>
/// Immutable data object passed to every ITemplateProcessor.
/// Contains everything needed to render a document — keeps processors lean and testable.
/// </summary>
public sealed record TemplateProcessingContext(
    /// <summary>Template metadata record from the database.</summary>
    TemplateMetadata Template,

    /// <summary>Full local file path to the template file (resolved before calling processor).</summary>
    string TemplatePath,

    /// <summary>Structured document data: replacements, tables, QR codes, barcodes, images.</summary>
    DocumentProcessingData Data,

    /// <summary>Desired output format: "pdf" | "docx" | "xlsx"</summary>
    string OutputFormat,

    /// <summary>
    /// When true, the processor MUST NOT upload to MinIO or write audit logs.
    /// Output bytes are streamed directly back to the caller.
    /// </summary>
    bool IsPreview = false
);
