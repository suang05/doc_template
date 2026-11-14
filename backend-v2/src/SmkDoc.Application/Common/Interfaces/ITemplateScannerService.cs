using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Common.Interfaces;

public interface ITemplateScannerService
{
    /// <summary>
    /// Scans a template file stream for {{placeholder}} keys.
    /// Extension determines parsing strategy: .docx / .xlsx / anything else (HTML).
    /// </summary>
    Task<List<string>> ScanPlaceholdersAsync(Stream stream, string fileExtension, CancellationToken ct);

    /// <summary>
    /// Scans a template file stream and returns rich categorized placeholders (Scalar, Tabular, Object, System).
    /// </summary>
    Task<List<TemplatePlaceholder>> ScanDetailedPlaceholdersAsync(Stream stream, string fileExtension, CancellationToken ct);
}
