using System.Diagnostics.CodeAnalysis;

namespace SmkDoc.Application.Modules.Rendering.Documents.DTOs;

/// <summary>
/// Result returned after document generation and MinIO persistence.
/// </summary>
public sealed record GenerateDocumentResultDto(
    Guid GenerationId,
    string? DocumentRef,
    string OutputFormat,
    long FileSizeBytes,
    string Sha256,
    [param: SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "JSON/OpenAPI wire DTO contract (supports relative API paths and pre-signed URLs).")]
    string Url,
    DateTimeOffset ExpiresAt
)
{
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON/OpenAPI wire DTO contract (supports relative API paths and pre-signed URLs).")]
    public string Url { get; init; } = Url;
}

/// <summary>
/// DTO representing an immutable document version in the legal audit trail.
/// </summary>
public sealed record DocumentVersionDto(
    Guid Id,
    Guid DocumentId,
    string DocumentRef,
    int Version,
    Guid? TemplateVersionId,
    Guid? GenerationLogId,
    string? ChangeNote,
    string? CreatedBy,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Security scan result for OpenXML DOCX files.
/// </summary>
public sealed record DocxScanResult(bool IsSafe, IReadOnlyList<string> Threats)
{
    public static DocxScanResult Safe() => new(true, []);
}
