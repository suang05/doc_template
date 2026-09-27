using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Validation;

namespace SmkDoc.Application.DTOs.Documents;

/// <summary>
/// Command for generating a document from a published template.
/// </summary>
public record GenerateDocumentCommand(
    JsonElement Data,
    string Output = "pdf",
    string? DocumentRef = null,
    string? ChangeNote = null,
    bool SkipValidation = false
);

/// <summary>
/// Result returned after document generation and MinIO persistence.
/// </summary>
public record GenerateDocumentResultDto(
    string Url,
    DateTimeOffset ExpiresAt,
    Guid GenerationId,
    string OutputFormat
);

/// <summary>
/// Query parameters for rendering a stateless preview document.
/// </summary>
public record PreviewDocumentQuery(
    JsonElement Data,
    string? Html = null
);

/// <summary>
/// Command for converting raw HTML directly to PDF bytes.
/// </summary>
public record HtmlToPdfCommand(
    string Html,
    string? HeaderHtml = null,
    string? FooterHtml = null
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

public record ValidatePayloadResult(
    bool IsValid,
    string TemplateSlug,
    int SchemaVersion,
    IReadOnlyList<ValidationErrorItem> Errors
) : PayloadValidationResultDto(IsValid, TemplateSlug, SchemaVersion, Errors);

/// <summary>
/// Security scan result for OpenXML DOCX files.
/// </summary>
public sealed record DocxScanResult(bool IsSafe, IReadOnlyList<string> Threats)
{
    public static DocxScanResult Safe() => new(true, []);
}
