using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public interface IDocumentAuditService
{
    Task LogValidationFailureAsync(
        Guid templateId,
        Guid templateVersionId,
        string dataJson,
        OutputFormat outputFormat,
        int elapsedMs,
        string errorMessage,
        CancellationToken ct = default);

    Task LogSuccessAsync(
        Guid generationId,
        Guid templateId,
        Guid templateVersionId,
        string dataJson,
        string outputKey,
        OutputFormat outputFormat,
        long fileSizeBytes,
        int elapsedMs,
        CancellationToken ct = default);
}
