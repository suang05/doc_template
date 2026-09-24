namespace SmkDoc.Application.Common.Interfaces;

public interface ITemplateScannerService
{
    /// <summary>
    /// Scans a template file stream for {{placeholder}} keys.
    /// Extension determines parsing strategy: .docx / .xlsx / anything else (HTML).
    /// </summary>
    Task<List<string>> ScanPlaceholdersAsync(Stream stream, string fileExtension, CancellationToken ct = default);
}
