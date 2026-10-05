using System.Text.Json;

namespace SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;

/// <summary>
/// Query for validating a JSON payload against a published template's schema contract (stateless, read-only dry-run).
/// </summary>
public record ValidateTemplatePayloadQuery(
    string Slug,
    JsonElement Data
);
