using System.Text.Json;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Services;

/// <summary>
/// Orchestrates report parameter extraction, multi-dataset query execution,
/// auto-binding by dataset alias, and explicit field mapping transformations (SSRS/JasperReports model).
/// </summary>
public interface IDataBindingOrchestrator
{
    Task<string> AssemblePayloadAsync(
        Template template,
        JsonElement callerParameters,
        CancellationToken ct = default);
}

