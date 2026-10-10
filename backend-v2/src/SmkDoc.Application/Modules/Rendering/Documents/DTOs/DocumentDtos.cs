using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Validation;

namespace SmkDoc.Application.Modules.Rendering.Documents.DTOs;

/// <summary>
/// Result returned after document generation and MinIO persistence.
/// </summary>
public record GenerateDocumentResultDto(
    Guid GenerationId,
    string? DocumentRef,
    string OutputFormat,
    long FileSizeBytes,
    string Sha256,
    string Url,
    DateTimeOffset ExpiresAt
);

/// <summary>
/// DTO representing an immutable document version in the legal audit trail.
/// </summary>
public record DocumentVersionDto(
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
/// Result DTO for pre-flight JSON schema validation.
/// </summary>
public record PayloadValidationResultDto(
    bool IsValid,
    string TemplateSlug,
    int SchemaVersion,
    IReadOnlyList<ValidationErrorItem> Errors
);

/// <summary>
/// Security scan result for OpenXML DOCX files.
/// </summary>
public sealed record DocxScanResult(bool IsSafe, IReadOnlyList<string> Threats)
{
    public static DocxScanResult Safe() => new(true, []);
}
