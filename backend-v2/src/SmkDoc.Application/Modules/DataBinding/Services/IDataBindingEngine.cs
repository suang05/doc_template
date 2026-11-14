using System.Text.Json;
using SmkDoc.Application.Modules.DataBinding.DTOs;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.DataBinding.Services;

/// <summary>
/// Unified, stateless data binding engine.
/// Coordinates external SQL dataset parallel resolution, field mapping transformations,
/// default values, math expressions, and JSON Schema Draft-07 validation.
/// Serves as the Single Source of Truth for both synchronous/asynchronous document generation and live preview.
/// </summary>
public interface IDataBindingEngine
{
    Task<BoundDocumentData> BindAndValidateAsync(
        Template templateEntity,
        TemplateVersion version,
        JsonElement rawPayload,
        bool skipValidation,
        CancellationToken ct = default);
}

