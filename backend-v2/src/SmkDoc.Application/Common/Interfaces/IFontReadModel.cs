using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Read model exposing platform-global and tenant-registered font descriptors for authoring and rendering engines.
/// </summary>
public interface IFontReadModel
{
    Task<IReadOnlyList<FontDescriptorDto>> ListRegisteredFontsAsync(ProjectId? projectId, CancellationToken ct);
}
