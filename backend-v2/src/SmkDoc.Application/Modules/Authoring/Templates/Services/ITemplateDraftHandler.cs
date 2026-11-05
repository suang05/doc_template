using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Authoring.Templates.Services;

/// <summary>
/// Polymorphic strategy handler for validating security and extracting placeholders
/// from format-specific template draft file streams (RAM-only).
/// </summary>
public interface ITemplateDraftHandler
{
    TemplateFormat Format { get; }

    Task ValidateSecurityAsync(Stream stream, CancellationToken ct = default);

    Task<List<string>> ExtractPlaceholdersAsync(Stream stream, CancellationToken ct = default);

    Task<List<TemplatePlaceholder>> ExtractDetailedPlaceholdersAsync(Stream stream, CancellationToken ct = default);
}
