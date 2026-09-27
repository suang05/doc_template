using SmkDoc.Application.DTOs.Templates;

namespace SmkDoc.Application.UseCases.Templates;

/// <summary>
/// Stateless use case for Monaco Editor interactions: loading HTML source,
/// live previewing in-memory buffers (zero side-effects), and syntax validation.
/// </summary>
public interface IHtmlStudioUseCase
{
    /// <summary>
    /// Loads the HTML source code of a published template for editing in Monaco Editor.
    /// </summary>
    Task<string> GetHtmlSourceAsync(Guid templateId, CancellationToken ct = default);

    /// <summary>
    /// Renders an in-memory HTML buffer directly to PDF bytes without writing to storage,
    /// generating audit logs, or modifying database records.
    /// </summary>
    Task<byte[]> PreviewHtmlBufferAsync(string htmlContent, string sampleDataJson, CancellationToken ct = default);

    /// <summary>
    /// Loads the full Monaco studio bundle (HTML, persisted SamplePayload, DataSchema, Version).
    /// </summary>
    Task<TemplateStudioDto> GetStudioBundleAsync(Guid templateId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the published template's schema contract (DataSchema & SamplePayload) for external integrations and form generators.
    /// </summary>
    Task<TemplateSchemaDto> GetTemplateSchemaAsync(Guid templateId, CancellationToken ct = default);

    /// <summary>
    /// Validates HTML syntax and extracts placeholders for the Monaco Editor interface.
    /// </summary>
    Task<TemplateValidationResultDto> ValidateHtmlAsync(string htmlContent, CancellationToken ct = default);
}

