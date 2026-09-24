using SmkDoc.Application.Common.Models;

namespace SmkDoc.Application.Common.Interfaces;

public interface IDocxSecurityScanner
{
    /// <summary>
    /// Scans a DOCX stream for VBA macros, embedded OLE objects, and suspicious
    /// external relationships. The stream position is restored after scanning.
    /// </summary>
    DocxScanResult Scan(Stream docxStream);
}
