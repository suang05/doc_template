using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.Documents;

namespace SmkDoc.Infrastructure.Security;

public sealed class DocxSecurityScannerService(ILogger<DocxSecurityScannerService> logger) : IDocxSecurityScanner
{
    public DocxScanResult Scan(Stream docxStream)
    {
        var startPos = docxStream.CanSeek ? docxStream.Position : -1L;
        var threats  = new List<string>();

        try
        {
            using var doc      = WordprocessingDocument.Open(docxStream, isEditable: false);
            var       mainPart = doc.MainDocumentPart;

            if (mainPart == null)
            {
                threats.Add("Document has no main document part.");
                return new DocxScanResult(false, threats);
            }

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
                var type = rel.RelationshipType?.ToLowerInvariant() ?? string.Empty;
                if (type.Contains("attachedtemplate") || type.Contains("frame") || type.Contains("oleobject"))
                    threats.Add($"Suspicious external relationship: {rel.Uri}");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[DocxSecurityScanner] Scan failed.");
            threats.Add($"Scan failed: {ex.Message}");
        }
        finally
        {
            if (startPos >= 0 && docxStream.CanSeek)
                docxStream.Position = startPos;
        }

        if (threats.Count > 0)
            logger.LogWarning("[DocxSecurityScanner] {Count} threat(s) found: {Threats}", threats.Count, string.Join("; ", threats));

        return new DocxScanResult(threats.Count == 0, threats);
    }
}
