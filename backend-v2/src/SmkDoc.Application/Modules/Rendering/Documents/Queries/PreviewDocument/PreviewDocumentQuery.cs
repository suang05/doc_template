using System;
using System.Text.Json;

namespace SmkDoc.Application.Modules.Rendering.Documents.Queries.PreviewDocument;

/// <summary>
/// Query parameters for rendering a stateless preview document.
/// Supports preview via raw HTML string, template slug, or template UUID.
/// </summary>
public sealed record PreviewDocumentQuery(
    JsonElement Payload,
    string? Html = null,
    string? Slug = null,
    Guid? TemplateId = null
);
