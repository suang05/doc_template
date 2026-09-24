namespace SmkDocServer.Domain.Models;

/// <summary>
/// Result returned from GenerateAndStoreDocumentAsync.
/// logId        — use with GET /api/Document/download/{logId} to stream the file via backend (avoids direct MinIO access from browser).
/// downloadUrl  — MinIO presigned URL (kept for external/API callers); browser should prefer the logId route.
/// previewUrl   — always a PDF (converted via Gotenberg) for the browser iframe.
/// When outputFormat is already "pdf", previewUrl == downloadUrl.
/// </summary>
public record DocumentGenerationResponse(
    string DownloadUrl,
    string PreviewUrl,
    Guid   LogId,
    string PreviewObjectName,
    string ExpiresIn = "1 Hour",
    string Message   = "Document generated and uploaded successfully."
);
