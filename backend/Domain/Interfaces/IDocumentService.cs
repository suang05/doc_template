using SmkDocServer.Domain.Models;

namespace SmkDocServer.Domain.Interfaces;

/// <summary>
/// Orchestrates document generation and preview using the Strategy Pattern (ITemplateProcessor).
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Generates a document, uploads it to MinIO, writes an audit log, and returns signed URLs.
    /// downloadUrl = native format; previewUrl = PDF (via Gotenberg) for iframe preview.
    /// </summary>
    Task<DocumentGenerationResponse> GenerateAndStoreDocumentAsync(DocumentGenerationRequest request);

    /// <summary>
    /// Generates a PDF preview and returns the raw bytes.
    /// IMPORTANT: Zero side effects — never uploads to MinIO or writes audit logs.
    /// </summary>
    Task<byte[]> PreviewDocumentAsync(DocumentGenerationRequest request);
}
