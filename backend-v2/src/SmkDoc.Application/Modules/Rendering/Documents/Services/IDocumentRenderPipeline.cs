using System.Text.Json;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.Rendering.Documents.Services;

public sealed record DocumentRenderResult(
    string PreparedDataJson,
    long FileSizeBytes,
    string Sha256Hex,
    int ElapsedMilliseconds);

public interface IDocumentRenderPipeline
{
    Task<DocumentRenderResult> RenderAndUploadAsync(
        Template template,
        TemplateVersion currentVersion,
        JsonElement payload,
        bool skipValidation,
        OutputFormat outputFormat,
        string outputKey,
        CancellationToken ct = default);
}

