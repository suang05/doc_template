namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public interface IDocumentVersioningService
{
    Task RecordVersionAsync(
        string documentRef,
        Guid templateId,
        Guid currentVersionId,
        Guid generationId,
        string? changeNote,
        CancellationToken ct = default);
}
