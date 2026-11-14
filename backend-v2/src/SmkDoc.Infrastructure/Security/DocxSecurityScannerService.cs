using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;

namespace SmkDoc.Infrastructure.Security;

public sealed partial class DocxSecurityScannerService(ILogger<DocxSecurityScannerService> logger) : IDocxSecurityScanner
{
    public DocxScanResult Scan(Stream docxStream)
    {
        var startPos = docxStream.CanSeek ? docxStream.Position : -1L;
        var threats = new List<string>();

        try
        {
            using var doc = WordprocessingDocument.Open(docxStream, isEditable: false);
            var mainPart = doc.MainDocumentPart;
            if (mainPart == null)
            {
                threats.Add("Document has no main document part.");
                return new DocxScanResult(false, threats);
            }

            InspectMainDocumentPart(mainPart, threats);
        }
        catch (Exception ex) when (ex is
            DocumentFormat.OpenXml.Packaging.OpenXmlPackageException or
            InvalidDataException or
            FormatException or
            IOException or
            InvalidOperationException)
        {
            RecordScanFailure(ex, threats);
        }
        finally
        {
            if (startPos >= 0 && docxStream.CanSeek)
                docxStream.Position = startPos;
        }

        if (threats.Count > 0 && logger.IsEnabled(LogLevel.Warning))
        {
            LogThreatsFound(logger, threats.Count, string.Join("; ", threats));
        }

        return new DocxScanResult(threats.Count == 0, threats);
    }

    private static void InspectMainDocumentPart(MainDocumentPart mainPart, List<string> threats)
    {
        if (mainPart.VbaProjectPart != null)
            threats.Add("VBA macro project detected.");

        foreach (var part in mainPart.Parts)
        {
            if (part.OpenXmlPart is EmbeddedObjectPart)
                threats.Add($"Embedded OLE object ({part.RelationshipId}).");
            else if (part.OpenXmlPart is EmbeddedPackagePart)
                threats.Add($"Embedded package ({part.RelationshipId}).");
        }

        foreach (var rel in mainPart.ExternalRelationships)
        {
            var type = rel.RelationshipType ?? string.Empty;
            if (type.Contains("attachedtemplate", StringComparison.OrdinalIgnoreCase) ||
                type.Contains("frame", StringComparison.OrdinalIgnoreCase) ||
                type.Contains("oleobject", StringComparison.OrdinalIgnoreCase))
            {
                threats.Add($"Suspicious external relationship: {rel.Uri}");
            }
        }
    }

    private void RecordScanFailure(Exception ex, List<string> threats)
    {
        LogScanFailed(logger, ex);
        threats.Add($"Scan failed: {ex.Message}");
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[DocxSecurityScanner] {Count} threat(s) found: {Threats}")]
    private static partial void LogThreatsFound(ILogger logger, int count, string threats);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "[DocxSecurityScanner] Scan failed.")]
    private static partial void LogScanFailed(ILogger logger, Exception ex);
}
