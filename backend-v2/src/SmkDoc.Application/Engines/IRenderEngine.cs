using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Engines;

public interface IRenderEngine
{
    RenderEngineType EngineType { get; }
    Task<byte[]> RenderAsync(Stream templateStream, string inputDataJson, OutputFormat outputFormat, CancellationToken ct = default);
}
