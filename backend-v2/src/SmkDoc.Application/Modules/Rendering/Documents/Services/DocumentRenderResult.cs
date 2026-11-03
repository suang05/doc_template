namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public sealed record DocumentRenderResult(
    string PreparedDataJson,
    long FileSizeBytes,
    string Sha256Hex,
    int ElapsedMilliseconds);
